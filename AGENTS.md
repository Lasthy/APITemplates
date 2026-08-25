# APITemplates — Agent Guide

This repository contains `dotnet new` templates for building **Modular Web API** (MWA) monoliths in ASP.NET (.NET 10). It ships two templates that work together:

| Template | Short Name | Type | Path |
|---|---|---|---|
| **Modular API Solution** | `modular-webapi` | Solution | `templates/mwa-solution/` |
| **Modular API Module** | `modular-webapi-module` | Project | `templates/mwa-module/` |

The solution template scaffolds the **Host** (entry-point web app) and the **Kernel** (shared library). The module template scaffolds a new module project that plugs into the solution.

---

## Architecture Overview

```
Solution Root (MyAPI.slnx)
├── Host/                    # ASP.NET Web App — entry point, pipeline, appsettings
│   └── Program.cs           # builder.AddKernel() + builder.AddData() + builder.AddModules()
├── Data/                    # Shared EF Core database infrastructure & contexts
│   ├── Configuration/       # DataSettings (dynamic settings for Database: Provider, ConnectionString)
│   ├── Extensions/          # WebApplicationBuilder extension (AddData)
│   └── Persistence/         # KernelDbContext (implements IKernelDbContext)
├── Kernel/                  # Shared class library — all cross-cutting concerns
│   ├── Common/              # Result pattern
│   ├── Configuration/       # Dynamic settings system (Controllers/, Features/)
│   ├── Constants/           # Application-wide constants (Roles)
│   ├── Context/             # Scoped operation context (auth, claims)
│   ├── Entities/            # Entity marker interfaces (audit, soft-delete, domain events)
│   ├── Events/              # EventBus abstraction + in-process impl + registry
│   ├── Extensions/          # WebApplicationBuilder extension (AddKernel)
│   ├── Health/              # Health check models (stub)
│   ├── Json/                # Centralized JSON serializer abstraction
│   ├── Messaging/           # Outbox/Inbox message persistence + processing
│   ├── Modules/             # IModule contract + module registration infrastructure
│   ├── Partitioning/        # Partition management for multi-instance deployments
│   ├── Persistence/         # IDbContext, IKernelDbContext + EF SaveChanges interceptors
│   ├── Requests/            # Request/Response + Dispatcher + Interceptor interfaces (stub)
│   ├── Ulid/                # Vendored Ulid implementation + EF/JSON/Type converters
│   ├── Workers/             # BaseWorker background service
│   └── ThirdParty/          # Third-party license files
├── Modules/                 # Directory where module projects live
│   ├── {ModuleName}/        # Module implementation (domain logic, handlers, endpoints)
│   └── {ModuleName}.Contracts/ # Companion public contracts (events, requests, DTOs)
├── create-module.ps1        # Helper script to scaffold, sln-add, and Host-reference a new module (PowerShell)
├── create-module.sh         # Helper script to scaffold, sln-add, and Host-reference a new module (Bash)
├── GlobalUsings.Shared.cs   # `global using DefaultId = System.Ulid;`
└── Directory.Build.props    # Auto-includes GlobalUsings.Shared.cs in all projects
```

### Key Design Principles

- **Kernel owns all cross-cutting concerns.** Modules depend on Kernel; Kernel never depends on modules.
- **Modules are self-contained projects.** Each module has its own `settings.{ModuleName}.json` and implements `IModule` for DI registration.
- **Companion Contract Projects for Cross-Module Communication.** Modules expose public contracts (events, requests, DTOs) via a companion `{ModuleName}.Contracts` project. A module may reference another module's `.Contracts` project, but **never** its implementation project.
- **Module activation is config-driven.** The `InstanceSettings` section in `settings.Instance.json` has an `ActiveModules` dictionary (`{ "ModuleName": true/false }`) controlling which modules are loaded at startup.
- **`DefaultId` is `System.Ulid` everywhere.** Defined in `GlobalUsings.Shared.cs` and propagated to all projects via `Directory.Build.props`.

---

## Project Breakdown

### Host (`templates/mwa-solution/Host/`)

The executable ASP.NET web application. This is the composition root.

| File | Purpose |
|---|---|
| `Program.cs` | Calls `builder.AddKernel()`, `builder.AddData()`, `builder.AddModules()`, `app.UseKernel()`, `app.UseModules()`, and mounts the OpenAPI viewer UI. |
| `Host.csproj` | Web SDK project, references Kernel and Data. Includes `Scalar.AspNetCore` and `Swashbuckle.AspNetCore`. |
| `appsettings.json` | Root dynamic settings — logging, allowed hosts (backed by `HostSettings`, feeds into `IConfiguration`). |
| `appsettings.{Environment}.json` | Environment-specific overrides for root dynamic settings (logging levels, etc.). |
| `appsettings.Application.json` | Dynamic settings for the application: `Environment`, `PartitionAmount`. |
| `appsettings.Application.{Environment}.json` | Optional environment overlay for application settings. |
| `appsettings.Data.json` | Dynamic settings for data/database: `Provider`, `ConnectionString`. |
| `appsettings.Data.{Environment}.json` | Optional environment overlay for database settings. |
| `appsettings.Instance.json` | Per-instance dynamic settings: OpenAPI viewer choice, partition range, active modules dictionary. |
| `appsettings.Messaging.json` | Dynamic settings for outbox messaging: processing interval, batch size, timeout, retry policy, cleanup retention. |

**Program.cs composition pattern:**
```csharp
builder.AddKernel();      // Register kernel services + kernel OpenAPI doc + dynamic settings in IConfiguration
builder.AddData();        // Register database options and DbContexts
builder.AddModules();     // Discover + register active modules
var app = builder.Build();
app.UseKernel();          // Map /openapi/kernel.json
app.UseModules();         // Map module endpoints + /openapi/{module}.json + event types
// Mount Scalar or Swagger UI (reads OpenApiViewer from appsettings.Instance.json)
```

**Configuration strategy:**
- **`appsettings.json`** (read by `HostSettings`) contains root host-level settings: logging, allowed hosts, and feeds directly into ASP.NET's `IConfiguration`.
- **`appsettings.Application.json`** (read by `ApplicationSettings`) contains runtime-mutable application settings.
- **`appsettings.Data.json`** (read by `DataSettings`) contains runtime-mutable database provider and connection string settings.
- **`appsettings.Instance.json`** (read by `InstanceSettings`) contains per-instance config: OpenAPI viewer choice (`OpenApiViewer`), partition range, active modules.
- Each **module has its own `appsettings.{ModuleName}.json`** with optional `appsettings.{ModuleName}.{Environment}.json` overlays for module-specific configuration.
- **All Dynamic Settings sections** automatically integrate into `builder.Configuration` (`IConfiguration`) and reload on changes.

### Data (`templates/mwa-solution/Data/`)

A shared class library for Entity Framework Core persistence implementations, database provider registration, and shared DbContext infrastructure.

- **`KernelDbContext`** — Concrete EF Core DbContext implementing `IKernelDbContext` (owns `DbSet<Message> Messages`).
- **`DataSettings`** — Dynamic settings for database provider and connection string (`settings.Data.json`).
- **`WebApplicationBuilderExtensions.AddData()`** — Registers `DataSettings`, wires up DbContext registration, and registers `IKernelDbContext` in DI.

### Kernel (`templates/mwa-solution/Kernel/`)

A class library (not a web project) that provides all shared infrastructure. It targets `net10.0` and references `Microsoft.AspNetCore.App`, Entity Framework Core, and OpenAPI packages.

---

## Feature Inventory & Status

### ✅ Implemented

#### Standardized Result (`Kernel/Common/`)
A railway-oriented result pattern for operation outcomes.

- **`Result`** — Base record: `IsSuccess`, `IsFailure`, `Messages[]`, `Error` (Exception, `[JsonIgnore]`). Factory methods: `Success()`, `Failure(Exception)`, `Success<T>(value)`, `Failure<T>(error)`. Has `Match<TOut>()`.
- **`Result<T>`** — Generic variant with `Value`. Supports `Map()`, `Bind()`, `Match()`.
- **`PagedResult<T>`** — Extends `Result<IEnumerable<T>>` with `Page` and `PageSize`.
- **`ResultExtensions`** — Async extension methods: `MapAsync`, `BindAsync`, `MatchAsync`.

> **Convention:** All operations should return `Result` or `Result<T>`. Never throw exceptions for expected failures — wrap them in `Result.Failure()`.

#### Dynamic Configuration (`Kernel/Configuration/`)
A runtime-mutable, convention-based settings system that reads from JSON files, supports environment overlays and environment variables, integrates directly into ASP.NET's `IConfiguration`, auto-persists changes, and raises events on mutation.

**File naming convention:**
- `appsettings.json` (base root settings) with optional `appsettings.{Environment}.json` (environment overlay).
- `appsettings.{SectionName}.json` (base section settings) with optional `appsettings.{SectionName}.{Environment}.json` (environment overlay).

**Environment Precedence:** Environment Variables (`{SectionName}__{Key}`, `APPSETTINGS__{SectionName}__{Key}`, or `SETTINGS__{SectionName}__{Key}`) > Environment JSON Overlay > Base JSON File. Writes (`SetValue`) always persist to the base file.

- **`DynamicSettingsSection`** — Abstract base class. Subclasses declare `SectionName` (or empty string for root settings) and expose strongly-typed properties via `GetValue<T>(key)` / `SetValue(key, value)`. Has `IsReadonly` guard, `Source` (base file path), `EnvironmentSource` (overlay path), `EnvironmentName`, `MergedValues`, `HasPendingChanges`, `TryGetValue<T>()`, and `Save()`. `SetValue()` auto-persists to the source file and raises `OnSettingsChanged`.
- **`DynamicSettings`** — Singleton registry of `DynamicSettingsSection` instances, keyed by `SectionName`. `AddSection<T>(instance)`, `GetSection<T>()`, `GetSection(name)`, `TryGetSection(...)`, `GetSections()`, `CommitChanges()`. Exposes `OnSectionAdded` and `OnSettingChanged`. When an `IEventBus` is wired (via `SetEventBus()` in `UseKernel()`), every value change automatically publishes a `SettingsChangedEvent`.
- **`DynamicSettingsConfigurationSource` / `DynamicSettingsConfigurationProvider`** — Bridges `DynamicSettings` into ASP.NET's `IConfiguration` (`builder.Configuration`). Flattens all registered sections into hierarchical keys (`{SectionName}:{Key}` and root keys like `AllowedHosts`, `Logging:...`), and triggers `OnReload()` when settings are modified or new sections are added.
- **`HostSettings`** — Root settings (`SectionName = ""`). Loaded from `appsettings.json` with `appsettings.{Environment}.json` overlay. Exposes `AllowedHosts` and feeds logging configuration to `IConfiguration`.
- **`SettingsChangedEvent`** — `record(SectionName, Key) : Event`. Published via `IEventBus` whenever any section value changes. Subscribe with `IEventHandler<SettingsChangedEvent>` to react to configuration changes.
- **`ApplicationSettings`** — `SectionName = "Application"`. Loaded from `appsettings.Application.json`. Exposes `Environment` and `PartitionAmount`.
- **`InstanceSettings`** — `SectionName = "Instance"`. Loaded from `appsettings.Instance.json`. Exposes `OpenApiViewer` (`"scalar"` | `"swagger"`), `Partition` (start/end range), and `ActiveModules` dictionary.
- **`GenericModuleSettings`** — Internal fallback for modules that don't define a custom `DynamicSettingsSection` subclass. `SectionName` is set to the module name.
- **`SettingsController`** (`Kernel/Configuration/Controllers/SettingsController.cs`) — ASP.NET Core controller exposing endpoints under route `api/v1/kernel/settings`. Protected by `[Authorize(Roles = Roles.System)]` and tagged in the `kernel` OpenAPI group.
- **Features** (`Kernel/Configuration/Features/`) — CQRS/Vertical slice requests and handlers: `GetSettings` / `GetSettingsHandler`, `UpdateSettings` / `UpdateSettingsHandler`.

> **Convention:** To add new settings, create a class inheriting from `DynamicSettingsSection`, declare `SectionName`, and expose properties using `GetValue<T>(nameof(Property))`. The system automatically loads the file `appsettings.{SectionName}.json` with optional `appsettings.{SectionName}.{Environment}.json` overlay and environment variables. For modules, place custom `DynamicSettingsSection` subclasses in the module assembly — they are auto-discovered and registered during `AddModule<T>()`. All settings are automatically available in `IConfiguration`.

**Example — creating a custom settings section:**
```csharp
public class PaymentSettings : DynamicSettingsSection
{
    public override string SectionName => "Payment";

    public string Gateway => GetValue<string>(nameof(Gateway));
    public decimal TaxRate => GetValue<decimal>(nameof(TaxRate));

    public PaymentSettings(IJsonSerializer jsonSerializer, string? environment = null)
        : base("appsettings.Payment.json", jsonSerializer, environment) { }
}
```
Corresponding file `appsettings.Payment.json`:
```json
{
  "Gateway": "stripe",
  "TaxRate": 0.1
}
```

#### Scope Context (`Kernel/Context/`)
Standardized context for the current operation scope, providing authentication information.

- **`IScopeContext`** — Interface: `UserId` (`DefaultId?`), `IsAuthenticated`, `Roles`, `Claims`, `GetClaim()`, `GetClaimValues()`, `IsInRole()`, `IsSystem`.
- **`WebScopeContext`** — Implementation backed by `IHttpContextAccessor`. Extracts user ID from `NameIdentifier` or `sub` claim, parses to `DefaultId`. Caches roles and claims. Has a protected constructor accepting `ClaimsPrincipal` for derived contexts. Implements `IsSystem => IsInRole(Constants.Roles.System)`.

> **Convention:** Inject `IScopeContext` into handlers and services. For non-web contexts (workers, event handlers), create a derived context.

#### Constants (`Kernel/Constants/`)
Application-wide constants.

- **`Roles`** — Static class with string role constants. `Roles.System = "System"` is the super-user role with unrestricted access.

> **System role:** Use `[Authorize(Roles = Roles.System)]` on kernel/host administrative endpoints (health details, dynamic settings mutation, partition info). The `IScopeContext.IsSystem` shorthand checks this role. See the [System Role](#system-role) section for full details.

#### Entity Interfaces (`Kernel/Entities/`)
Marker interfaces for entity cross-cutting concerns. Used by EF SaveChanges interceptors.

- **`IAuditable`** — `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy` (uses `DefaultId`).
- **`ISoftDeletable`** — `DeletedAt`, `DeletedBy` (uses `DefaultId`).
- **`IHasDomainEvents`** — `DomainEvents` (read-only collection of `Event`), `ClearDomainEvents()`.

> **Convention:** Entities should implement these interfaces as needed. The Kernel's SaveChanges interceptors automatically handle `IAuditable` and `ISoftDeletable` entities. `IHasDomainEvents` entities have their events dispatched on save.

#### Event System (`Kernel/Events/`)
A complete event bus abstraction with in-process default implementation.

- **`IEvent`** — Marker: `Id` (`DefaultId`), `OccurredAt` (`DateTime`).
- **`Event`** — Abstract record implementing `IEvent`. `Id` auto-generates a new ULID, `OccurredAt` uses `DateTime.UtcNow`.
- **`IEventBus`** — `PublishAsync<T>(event, ct)`.
- **`InProcessEventBus`** — Default implementation; resolves `IEventHandler<T>` from DI and invokes them sequentially.
- **`IEventHandler<TEvent>`** — `HandleAsync(event, ct)`.
- **`IEventTypeRegistry` / `EventTypeRegistry`** — Maps event CLR type full names to `Type` objects. Used for inbox/outbox deserialization. Supports `Register<T>()` and `RegisterFromAssembly()`.
- **`WebApplicationBuilderExtensions`** — `AddEventBus()` registers `InProcessEventBus` and `IEventTypeRegistry`. Called automatically by `AddKernel()` — use directly only for advanced scenarios.

> **Convention:** Define events as records inheriting from `Event`. Register handlers as `IEventHandler<YourEvent>`. The module registration (`AddModule<T>`) automatically scans and registers all `IEventHandler<>` implementations from the module assembly. Event types are registered into `IEventTypeRegistry` during `UseModules()` (post-build) via `RegisterFromAssembly()`.

#### Module System (`Kernel/Modules/`)
Infrastructure for pluggable modules.

- **`IModule`** — Contract with:
  - `static abstract string Name` — module identifier; must match the key in `ActiveModules`.
  - `static abstract void Register(IServiceCollection, IConfiguration)` — DI registration, called at build time.
  - `static abstract void MapEndpoints(IEndpointRouteBuilder)` — route registration, called at runtime by `UseModules()`.
- **`ModuleRegistry`** — Singleton that accumulates `ModuleDescriptor` records (name, assembly, `MapEndpoints` delegate) at build time. Used by `UseModules()` to iterate modules post-build without anti-pattern `BuildServiceProvider()` calls.
- **`ModuleDescriptor`** — `record(Name, Assembly, MapEndpoints)` representing a registered module.
- **`WebApplicationBuilderExtension`**:
  - `AddModules()` — Reads `InstanceSettings.ActiveModules`, reflects into loaded assemblies to find `IModule` implementations, calls `AddModule<T>()` for each active module.
  - `AddModule<T>()` — Auto-discovers `DynamicSettingsSection` subclasses in the module assembly and registers them. Falls back to `GenericModuleSettings` if no custom section exists. Calls `T.Register()`, registers named OpenAPI document (`builder.Services.AddOpenApi(T.Name)`), scans event handlers, records in `ModuleRegistry`.
  - `UseModules(WebApplication app)` — Post-build: iterates `ModuleRegistry`, calls `IEventTypeRegistry.RegisterFromAssembly()` per module, calls `module.MapEndpoints(app)`, maps `/openapi/{module.Name}.json`.

> **Convention:** Each module project must have exactly one class implementing `IModule`. The class name doesn't matter; it's discovered by interface. The module assembly name must match the key in `ActiveModules`.

#### Messaging / Outbox (`Kernel/Messaging/`)
A channel-based message persistence, partition-sharded background processing, and retention system (outbox pattern).

- **`Message`** — Entity with `Id`, `Type` (CLR type name), `Content` (serialized JSON), `Channel` (enum), `OccurredAt`, `ProcessedAt`, `ProcessingAt`, `RetryCount`, `NextRetryAt`, `Partition`, `LastError`. Exposes state helper methods: `MarkProcessing()`, `MarkProcessed()`, `MarkFailed()`, `MarkTimedOut()`, `IsProcessingTimedOut()`.
- **`MessageChannel`** — Enum: `Event`, `Email`.
- **`IMessageWriter` / `MessageWriter`** — Serializes content, assigns a partition via `IPartitionManager`, persists to `IKernelDbContext.Messages`.
- **`IMessageProcessor`** — Per-channel processor: `GetChannel()`, `ProcessMessage()`.
- **`EventMessageProcessor`** — Processes `MessageChannel.Event` messages: resolves event type via `IEventTypeRegistry`, deserializes, publishes to `IEventBus` within a `TransactionScope`.
- **`IMessageDispatcher` / `MessageDispatcher`** — Routes messages to the correct `IMessageProcessor` by channel, returning `Task<Result>`.
- **`MessageProcessingWorker`** — Hosted `BaseWorker` running on `ProcessingIntervalSeconds`:
  - **Partitioning**: Only processes messages matching `instanceSettings.Partition` (`[Start..End]`).
  - **Stuck/Timeout Recovery**: Detects messages in `ProcessingAt` exceeding `ProcessingTimeoutSeconds` and marks them timed out for retry.
  - **Exponential Backoff**: Reschedules failed messages with exponential delay ($2^{\text{retry}-1} \times \text{InitialDelay}$, capped at `MaxRetryDelaySeconds`) until `MaxRetryAttempts`.
- **`MessageCleanupWorker`** — Hosted `BaseWorker` running on `CleanupIntervalHours`, deleting processed messages older than `CleanupRetentionDays` (and optionally permanently failed messages).
- **`MessagingSettings`** — Dynamic settings loaded from `settings.Messaging.json`.

> **Note:** The `Message` class and related types use `Kernel.Boxing` namespace (not `Kernel.Messaging`). This is the current convention.

#### JSON Serialization (`Kernel/Json/`)
Centralized JSON serializer with consistent configuration.

- **`IJsonSerializer`** — `Serialize(obj)`, `SerializeToElement(obj)`, `Deserialize<T>(json)`, `Deserialize(json, type)`.
- **`JsonSerializer`** — Uses `System.Text.Json` with a `UlidJsonConverter` and `WriteIndented = true`.
- Custom exception types: `JsonSerializationException`, `JsonDeserializationException`.

> **Convention:** Always use `IJsonSerializer` for JSON operations — never use `System.Text.Json.JsonSerializer` directly. This ensures consistent Ulid handling and future converter additions.

#### Background Workers (`Kernel/Workers/`)
- **`BaseWorker`** — Abstract `BackgroundService` with a configurable `Interval` (default 1 min) or one-shot execution mode (`isOneShot: true`). Creates a DI scope per iteration (or once if one-shot), calls `ExecuteJobAsync()`, handles errors with structured logging. Supports `RequestCancellation()` for graceful self-stop.

> **Convention:** Module workers inherit from `BaseWorker`. Override `ExecuteJobAsync(IServiceProvider, CancellationToken)`. Resolve scoped services from the provided `IServiceProvider`. For startup or data migration tasks that should run once, pass `isOneShot: true` to the base constructor.

#### Partitioning (`Kernel/Partitioning/`)
For distributing work across multiple API instances.

- **`IPartitionManager`** — `GetRandomPartition()`.
- **`PartitionManager`** — Depends on `ApplicationSettings`. Returns a random `int` in `[0, PartitionAmount - 1]` using `Random.Shared`.

> **Convention:** Use `IPartitionManager.GetRandomPartition()` when writing outbox messages to assign a partition. Each instance owns a range of partition numbers configured in `settings.Instance.json` for processing.

#### Global Usings (`GlobalUsings.Shared.cs`)
```csharp
global using DefaultId = System.Ulid;
```
Propagated to all projects via `Directory.Build.props`. This means `DefaultId` is the standard identifier type everywhere.

#### Ulid Support (`Kernel/Ulid/`)
Vendored Ulid implementation (from `Cysharp/Ulid`) with converters:
- `UlidJsonConverter` — For System.Text.Json.
- `UlidEfcoreConverter` — For Entity Framework Core value conversion.
- `UlidTypeConverter` — For general .NET type conversion.
- `RandomProvider` — Cryptographic random provider for Ulid generation.

#### Request/Response Pattern (`Kernel/Requests/`)
A MediatR-style CQRS pattern with an attribute-based interceptor pipeline.

- **`IRequest<TResponse>`** — Marker interface for requests returning `Result<TResponse>`.
- **`IRequestHandler<TRequest, TResponse>`** — Handler interface: `HandleAsync(request, ct)`.
- **`IDispatcher` / `Dispatcher`** — Resolves the appropriate `IRequestHandler<,>`, inspects `[RequestInterceptor]` attributes on the handler, builds an ordered interceptor pipeline, and executes it. Auto-unwraps exceptions into `Result.Failure<TResponse>`.
- **`IRequestInterceptor`** — Contract: `InterceptAsync(request, next, ct)`.
- **`RequestInterceptorAttribute`** — Base attribute for applying interceptors to handlers. Exposes `Order` (`int`, default `0`, lower numbers execute outermost).
- **`InterceptAttribute<TInterceptor>` / `InterceptAttribute`** — Generic and type-based attributes for attaching arbitrary interceptors.
- **`LoggingAttribute` & `LoggingInterceptor`** — Logs incoming request payload, measures execution duration with `Stopwatch`, and logs outcome. Configurable `LogLevel` and `IncludePayload`.
- **`TransactionAttribute` & `TransactionInterceptor`** — Wraps handler execution in a `TransactionScope` (`TransactionScopeAsyncFlowOption.Enabled`). Commits on `Result.IsSuccess`, rolls back on `Result.IsFailure` or exceptions. Configurable `IsolationLevel`, `TimeoutSeconds`, and `ScopeOption`.

> **Convention:** Place feature requests and handlers in `Features/` vertical slices. Decorate handler classes with `[Logging]`, `[Transaction]`, or `[Intercept<T>]` with optional `Order = n` and custom parameters. Module request handlers are auto-discovered during `AddModule<T>()`.

#### EF Core Persistence Abstractions (`Kernel/Persistence/`)
- **`IDbContext`** — Common DbContext interface declaring standard EF Core operations (`Database`, `ChangeTracker`, `Set<T>()`, `Entry<T>()`, `SaveChanges()`, `SaveChangesAsync()`).
- **`IKernelDbContext`** — Inherits `IDbContext`, exposes `DbSet<Message> Messages { get; }`.
- **`AuditSaveChangesInterceptor`** — Intercepts `SaveChanges` / `SaveChangesAsync` to auto-populate `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy` on `IAuditable` entities using `IScopeContext`.
- **`SoftDeleteSaveChangesInterceptor`** — Intercepts `SaveChanges` / `SaveChangesAsync` to convert `EntityState.Deleted` into `EntityState.Modified` and populate `DeletedAt`, `DeletedBy` on `ISoftDeletable` entities using `IScopeContext`.
- **`DispatchDomainEventsSaveChangesInterceptor`** — Intercepts `SavedChanges` / `SavedChangesAsync` to collect and dispatch `DomainEvents` from `IHasDomainEvents` entities via `IEventBus` and clear them.
- **`DbContextOptionsBuilderExtensions`** — Helper extension methods (`UseConfiguredDatabase`, `AddKernelInterceptors`) for configuring providers and attaching interceptors.

#### EF Core Persistence Implementation (`Data/Persistence/`)
- **`KernelDbContext`** — Implements `IKernelDbContext`, inherits `DbContext`. Registered in DI via `builder.AddData()`.

#### Health Checks (`Kernel/Health/`)
A modular health reporting system that groups health checks by module, calculates aggregate status levels, provides liveness/readiness probes, and exposes detailed diagnostics.

- **`HealthStatus`** — Enum (`Healthy`, `Degraded`, `Unhealthy`) with string JSON serialization and `Combine` extension method.
- **`HealthCheckResult`** — Result record for individual check outcomes (`Status`, `Description`, `Exception`, `Data`). Factory methods: `Healthy()`, `Degraded()`, `Unhealthy()`.
- **`HealthReportComponent`** — Diagnostic report for an executed check (`Name`, `Status`, `Description`, `Duration`, `Data`, `Error`).
- **`ModuleHealthReport`** — Aggregates component checks and status for a specific module (`ModuleName`, `Status`, `Duration`, `Components`).
- **`HealthReport`** — Overall system health report (`Status`, `TotalDuration`, `Timestamp`, `Modules`).
- **`IHealthCheck`** — Interface: `Name`, `CheckHealthAsync(ct)`. Implement this in any module or Kernel layer.
- **`IHealthCheckRegistry` / `HealthCheckRegistry`** — Thread-safe singleton registry storing health checks grouped by module. Supports type registration, delegates, and assembly scanning.
- **`IHealthService` / `HealthService`** — Scoped service that executes health checks with duration tracking (`Stopwatch`), catches unhandled exceptions, and compiles reports.
- **`MemoryHealthCheck`** (`Kernel/Health/Checks/`) — Built-in health check monitoring memory allocation and GC collection metrics.
- **`DatabaseHealthCheck`** (`Data/Health/`) — Built-in health check testing database connectivity via `IKernelDbContext.Database.CanConnectAsync()`.
- **`HealthController`** (`Kernel/Health/Controllers/HealthController.cs`) — ASP.NET Core controller at route `api/v1/kernel/health` (tagged in `kernel` OpenAPI group):
  - `GET api/v1/kernel/health/live` (`[AllowAnonymous]`) — Liveness probe (200 OK).
  - `GET api/v1/kernel/health/ready` (`[AllowAnonymous]`) — Readiness probe (200 OK or 503 Service Unavailable).
  - `GET api/v1/kernel/health/report` (`[Authorize(Roles = Roles.System)]`) — Full system health report breakdown.
  - `GET api/v1/kernel/health/report/{moduleName}` (`[Authorize(Roles = Roles.System)]`) — Detailed module health report.
- **Features** (`Kernel/Health/Features/`) — CQRS requests and handlers: `GetHealthReport` / `GetHealthReportHandler`, `GetModuleHealthReport` / `GetModuleHealthReportHandler`.

> **Convention:** Place module-specific health checks anywhere in your module assembly implementing `IHealthCheck`. The module registration (`AddModule<T>`) automatically discovers and registers all `IHealthCheck` implementations under the module name. Public health probes (`/live`, `/ready`) do not require authorization, whereas detailed reports (`/report`, `/report/{moduleName}`) require `Roles.System`.

---

## Configuration Architecture

The configuration architecture is unified around **Dynamic Settings**, which automatically feeds into ASP.NET's **`IConfiguration`** (`builder.Configuration`):

```
Host/
├── appsettings.json                           # Root dynamic settings: AllowedHosts, Logging
├── appsettings.{Environment}.json             # Optional root environment overlay
├── appsettings.Application.json               # DynamicSettings: Environment, PartitionAmount
├── appsettings.Application.{Environment}.json # Optional environment overlay
├── appsettings.Data.json                      # DynamicSettings: Provider, ConnectionString
├── appsettings.Data.{Environment}.json         # Optional environment overlay
├── appsettings.Instance.json                  # DynamicSettings: OpenApiViewer, Partition, ActiveModules
└── appsettings.Instance.{Environment}.json    # Optional environment overlay

Modules/
└── {ModuleName}/
    ├── appsettings.{ModuleName}.json          # Module-specific dynamic settings
    └── appsettings.{ModuleName}.{Environment}.json  # Optional environment overlay
```

### appsettings.json (HostSettings & ASP.NET Configuration)
```json
{
  "AllowedHosts": "*",
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

### appsettings.Application.json (ApplicationSettings)
```json
{
  "Environment": "Development",
  "PartitionAmount": 100
}
```

### appsettings.Data.json (DataSettings)
```json
{
  "Provider": "Sqlite",
  "ConnectionString": "Data Source=kernel.db"
}
```

### appsettings.Instance.json (InstanceSettings)
```json
{
  "OpenApiViewer": "scalar",
  "Partition": { "Start": 0, "End": 99 },
  "ActiveModules": {
    "MyModule": true,
    "AnotherModule": false
  }
}
```

### Environment Overlays & Environment Variables
- **File Overlays**: For any section, create `appsettings.{Section}.{Environment}.json` in the same directory (or `appsettings.{Environment}.json` for root). Values in the environment file override the base file.
- **Environment Variables**: Set `{Section}__{Key}`, `APPSETTINGS__{Section}__{Key}`, or `SETTINGS__{Section}__{Key}` (e.g. `Data__ConnectionString=...`). Environment variables have top precedence.
- **Persistence**: Writes (`SetValue`) always persist cleanly to the base file without affecting environment files or variables.
- **IConfiguration**: All dynamic settings (Root, Application, Instance, Messaging, Data, and Modules) are flattened and accessible via `IConfiguration[key]` and reload on mutation.

### OpenAPI Documents
Each active component gets its own named OpenAPI document:
- `kernel` → `/openapi/kernel.json` — registered by `AddKernel()`
- `{ModuleName}` → `/openapi/{ModuleName}.json` — registered by `AddModule<T>()`

All documents are exposed in the viewer UI (Scalar or Swagger) mounted at `/scalar` or `/docs`.

### Module Settings
Each module should own its own `settings.{ModuleName}.json` file. Module settings are either:
- Auto-loaded as a `GenericModuleSettings` section (fallback).
- Loaded via a custom `DynamicSettingsSection` subclass in the module assembly (recommended — auto-discovered during `AddModule<T>()`).

Module `.csproj` files include `<Content Include="settings.*.json" CopyToOutputDirectory="PreserveNewest" />` to ensure settings files are in the build output.

---

## Module Template (`templates/mwa-module/`)

Scaffolds a new module under `Modules/{ModuleName}/` and its companion contracts project under `Modules/{ModuleName}.Contracts/`.

```
Modules/
├── {ModuleName}/                              # Module implementation
│   ├── Features/                              # CQRS requests & handlers (vertical slices)
│   ├── Controllers/                           # Optional controller classes
│   ├── Entities/                              # Domain entity definitions
│   ├── Events/                                # Internal events & event handlers
│   ├── Workers/                               # Module background workers
│   ├── Configuration/                         # DynamicSettingsSection subclasses
│   ├── Persistence/                           # EF Core DbContext & configurations
│   ├── {ModuleName}Module.cs                  # IModule entry point
│   ├── appsettings.{ModuleName}.json          # Module dynamic settings (base)
│   ├── appsettings.{ModuleName}.Development.json # Development overlay
│   └── {ModuleName}.csproj                    # References Kernel & {ModuleName}.Contracts
│
└── {ModuleName}.Contracts/                    # Companion public contracts (shareable)
    ├── Dtos/                                  # Public DTO records
    ├── Events/                                # Public domain event records (inheriting Event)
    ├── Requests/                              # Public CQRS requests (implementing IRequest<T>)
    └── {ModuleName}.Contracts.csproj          # References Kernel only
```

### Module Scaffolding & Activation

#### Creating a Module (Automated via Script)
Run the helper script from the solution root:
```powershell
# PowerShell
./create-module.ps1 Orders
```
or
```bash
# Bash
./create-module.sh Orders
```
The script automatically:
1. Executes `dotnet new mwa-module -n Orders` (generating both `Orders` and `Orders.Contracts`)
2. Adds both projects to the `.slnx` solution under the `Modules` solution folder (`dotnet sln add Modules/Orders/Orders.csproj -s Modules` and `dotnet sln add Modules/Orders.Contracts/Orders.Contracts.csproj -s Modules`)
3. Adds the project reference to `Host/Host.csproj` (`dotnet add Host/Host.csproj reference Modules/Orders/Orders.csproj`)

---

## Cross-Module Communication

Cross-module communication follows the **Companion Contract Projects (`*.Contracts`)** pattern:

### 1. Synchronous Queries/Commands (via `IDispatcher`)
When `Notifications` needs data from `Orders`:
1. Reference `Orders.Contracts` in `Notifications.csproj`:
   ```xml
   <ItemGroup>
     <ProjectReference Include="../Orders.Contracts/Orders.Contracts.csproj" />
   </ItemGroup>
   ```
2. In `Notifications`, inject `IDispatcher` and send the request:
   ```csharp
   var result = await _dispatcher.DispatchAsync(new GetOrderSummary(orderId), ct);
   if (result.IsSuccess)
   {
       var summary = result.Value; // OrderSummaryDto
   }
   ```
3. `Dispatcher` discovers and invokes `GetOrderSummaryHandler` inside the `Orders` module at runtime.

### 2. Asynchronous Pub/Sub Events (via `IEventBus`)
When `Orders` creates an order and `Notifications` needs to react:
1. `Orders` publishes an event defined in `Orders.Contracts`:
   ```csharp
   await _eventBus.PublishAsync(new OrderPlacedEvent(order.Id, order.Total), ct);
   ```
2. `Notifications` implements `IEventHandler<OrderPlacedEvent>`:
   ```csharp
   public class SendOrderEmailHandler(ILogger<SendOrderEmailHandler> logger) : IEventHandler<OrderPlacedEvent>
   {
       public async Task HandleAsync(OrderPlacedEvent @event, CancellationToken ct = default)
       {
           logger.LogInformation("Order placed: {OrderId}", @event.Id);
       }
   }
   ```

## System Role

The `Roles.System` constant (`"System"`) represents a super-user role with unrestricted access to all operations, including kernel-level administrative endpoints.

### Files
- **`Kernel/Constants/Roles.cs`** — `public static class Roles { public const string System = "System"; }`
- **`IScopeContext.IsSystem`** — Shorthand for `IsInRole(Roles.System)`
- **`WebScopeContext.IsSystem`** — Implemented as `IsInRole(Constants.Roles.System)`

### Usage

**In Minimal API endpoints (recommended for kernel/host routes):**
```csharp
app.MapGet("/admin/settings", ...)
   .RequireAuthorization(p => p.RequireRole(Roles.System));
```

**In controller actions:**
```csharp
[Authorize(Roles = Roles.System)]
public IActionResult GetInstanceInfo() { ... }
```

**In service/handler code:**
```csharp
if (!_context.IsSystem)
    return Result.Failure(new UnauthorizedAccessException("System role required."));
```

### What requires System role

Kernel/Host administrative operations that should only be accessible to system-level clients:

| Endpoint / Operation | Reason |
|---|---|
| Health details endpoint (per-component breakdown) | Internal diagnostics |
| Dynamic settings read/write endpoints (when exposed via HTTP) | Can affect runtime behavior |
| Partition management endpoints | Affects distributed processing |
| Active modules management (toggle on/off at runtime) | Affects loaded features |
| Outbox message status and retry endpoints | Internal infrastructure |

> **Convention:** Public health checks (liveness, readiness) do **not** require System role — only detailed health reports do. Business module endpoints should use their own domain-specific roles, not `Roles.System`.

---

## Coding Conventions

### Naming
- **Identifiers** are always `DefaultId` (= `Ulid`). Never use `Guid`, `int`, or `string` for entity IDs.
- **Namespaces** follow the folder structure.
- **Features & Requests**: Place requests/commands/queries in a `Features/` folder. Do **not** append `Request` to the record/class name; name them directly as verbs/actions (e.g., `GetSettings`, `UpdateSettings`, `CreateOrder`). The handler is named with a `Handler` suffix (e.g. `GetSettingsHandler`, `UpdateSettingsHandler`, `CreateOrderHandler`).
- **Endpoints & Routing**: All API endpoints must start with `api/<version>/` (e.g., `api/v1/kernel/settings`, `api/v1/orders`). Controllers use `[Route("api/v1/...")]`.
- **Extension methods** for `WebApplicationBuilder` go in files named `WebApplicationBuilderExtensions.cs` within their feature folder.

### Patterns
- **Result pattern** for all operation returns — no throwing expected exceptions.
- **Interface-first** — features expose an interface (e.g. `IEventBus`, `IJsonSerializer`, `IPartitionManager`) with a default implementation. Consumers depend on the interface.
- **Convention-based DI registration** — Module registration auto-scans assemblies for `IEventHandler<>` implementations.

### Project Dependencies
```
Host → Kernel, Data, Modules (active)
Data → Kernel
Module → Kernel, Module.Contracts (and Data if using EF DbContexts)
Module.Contracts → Kernel
ModuleA → ModuleB.Contracts (never ModuleB implementation)
Kernel → (no project references, only NuGet packages)
```

### Target Framework
- All projects target `net10.0`.
- Nullable reference types and implicit usings are enabled everywhere.

---

## Where to Put Things

| What you're adding | Where it goes |
|---|---|
| New cross-cutting infrastructure (e.g., caching, rate limiting) | `Kernel/{FeatureName}/` |
| New feature request & handler | `Kernel/{FeatureName}/Features/` or `Modules/{ModuleName}/Features/` |
| New API controller | `Kernel/{FeatureName}/Controllers/` or `Modules/{ModuleName}/Controllers/` |
| New entity marker interface | `Kernel/Entities/` |
| New EF interceptor | `Kernel/Persistence/` |
| New message channel + processor | `Kernel/Messaging/` (namespace: `Kernel.Boxing`) |
| New global type alias | `GlobalUsings.Shared.cs` |
| New kernel DI registration | `Kernel/Extensions/WebApplicationBuilderExtensions.cs` (`AddKernel`) |
| New kernel pipeline step | `Kernel/Extensions/WebApplicationBuilderExtensions.cs` (`UseKernel`) |
| New kernel extension method (feature-local) | Feature-local `WebApplicationBuilderExtensions.cs` |
| New role constant | `Kernel/Constants/Roles.cs` |
| New host-level setting | `Host/settings.json` (via `HostSettings` / `DynamicSettings` / `IConfiguration`) |
| New runtime-mutable setting | New `DynamicSettingsSection` subclass with `SectionName` + `settings.{SectionName}.json` file |
| New module | `Modules/{ModuleName}/` (use `modular-webapi-module` template) |
| Module endpoints | `IModule.MapEndpoints(IEndpointRouteBuilder)` inside the module project |
| Module-specific entities, handlers, services | Inside the module project |
| Host pipeline configuration (viewers, middleware) | `Host/Program.cs` |
| Global NuGet package settings | `Directory.Build.props` |
| Host settings (root: logging, allowed hosts) | `Host/appsettings.json` (backed by `HostSettings` into `IConfiguration`) |
| Per-instance settings | `Host/appsettings.Instance.json` via `InstanceSettings` |
| Application-level mutable settings | `Host/appsettings.Application.json` via `ApplicationSettings` |
| Database / Data dynamic settings | `Host/appsettings.Data.json` via `DataSettings` |
| Module-specific settings | `Modules/{ModuleName}/appsettings.{ModuleName}.json` |
| Third-party licenses | `Kernel/ThirdParty/Licenses/` |
