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
├── Modules/                 # Directory where module projects live (.gitkeep placeholder)
├── create-module.ps1        # Helper script to scaffold, sln-add, and Host-reference a new module (PowerShell)
├── create-module.sh         # Helper script to scaffold, sln-add, and Host-reference a new module (Bash)
├── GlobalUsings.Shared.cs   # `global using DefaultId = System.Ulid;`
└── Directory.Build.props    # Auto-includes GlobalUsings.Shared.cs in all projects
```

### Key Design Principles

- **Kernel owns all cross-cutting concerns.** Modules depend on Kernel; Kernel never depends on modules.
- **Modules are self-contained projects.** Each module has its own `settings.{ModuleName}.json` and implements `IModule` for DI registration.
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
| `appsettings.json` | ASP.NET built-in configuration — logging, allowed hosts. |
| `appsettings.{Environment}.json` | ASP.NET environment-specific overrides (logging levels, etc.). |
| `settings.Application.json` | Dynamic settings for the application: `Environment`, `PartitionAmount`. |
| `settings.Application.{Environment}.json` | Optional environment overlay for application settings. |
| `settings.Data.json` | Dynamic settings for data/database: `Provider`, `ConnectionString`. |
| `settings.Data.{Environment}.json` | Optional environment overlay for database settings. |
| `settings.Instance.json` | Per-instance dynamic settings: OpenAPI viewer choice, partition range, active modules dictionary. |

**Program.cs composition pattern:**
```csharp
builder.AddKernel();      // Register kernel services + kernel OpenAPI doc
builder.AddData();        // Register database options and DbContexts
builder.AddModules();     // Discover + register active modules
var app = builder.Build();
app.UseKernel();          // Map /openapi/kernel.json
app.UseModules();         // Map module endpoints + /openapi/{module}.json + event types
// Mount Scalar or Swagger UI (reads OpenApiViewer from settings.Instance.json)
```

**Configuration strategy:**
- The **main `appsettings.json`** is used solely by ASP.NET's built-in `IConfiguration` for static host-level settings: logging, allowed hosts.
- **`settings.Application.json`** (read by `ApplicationSettings`) contains runtime-mutable application settings.
- **`settings.Data.json`** (read by `DataSettings`) contains runtime-mutable database provider and connection string settings.
- **`settings.Instance.json`** (read by `InstanceSettings`) contains per-instance config: OpenAPI viewer choice (`OpenApiViewer`), partition range, active modules.
- Each **module has its own `settings.{ModuleName}.json`** with optional `settings.{ModuleName}.{Environment}.json` overlays for module-specific configuration.

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
A runtime-mutable, convention-based settings system that reads from JSON files, supports environment overlays, auto-persists changes, and raises events on mutation.

**File naming convention:** `settings.{SectionName}.json` (base) with optional `settings.{SectionName}.{Environment}.json` (environment overlay). Environment values override base values; writes always go to the base file.

- **`DynamicSettingsSection`** — Abstract base class. Subclasses declare `SectionName` and expose strongly-typed properties via `GetValue<T>(key)` / `SetValue(key, value)`. Has `IsReadonly` guard, `Source` (base file path), `EnvironmentSource` (overlay path), `HasPendingChanges`, `TryGetValue<T>()`, and `Save()`. `SetValue()` auto-persists to the source file and raises `OnSettingsChanged`.
- **`DynamicSettings`** — Singleton registry of `DynamicSettingsSection` instances, keyed by `SectionName`. `AddSection<T>(instance)`, `GetSection<T>()`, `GetSection(name)`, `CommitChanges()`. When an `IEventBus` is wired (via `SetEventBus()` in `UseKernel()`), every value change automatically publishes a `SettingsChangedEvent`.
- **`SettingsChangedEvent`** — `record(SectionName, Key) : Event`. Published via `IEventBus` whenever any section value changes. Subscribe with `IEventHandler<SettingsChangedEvent>` to react to configuration changes.
- **`ApplicationSettings`** — `SectionName = "Application"`. Loaded from `settings.Application.json`. Exposes `Environment` and `PartitionAmount`.
- **`InstanceSettings`** — `SectionName = "Instance"`. Loaded from `settings.Instance.json`. Exposes `OpenApiViewer` (`"scalar"` | `"swagger"`), `Partition` (start/end range), and `ActiveModules` dictionary.
- **`GenericModuleSettings`** — Internal fallback for modules that don't define a custom `DynamicSettingsSection` subclass. `SectionName` is set to the module name.
- **`SettingsController`** (`Kernel/Configuration/Controllers/SettingsController.cs`) — ASP.NET Core controller exposing endpoints under route `api/v1/kernel/settings`. Protected by `[Authorize(Roles = Roles.System)]` and tagged in the `kernel` OpenAPI group.
- **Features** (`Kernel/Configuration/Features/`) — CQRS/Vertical slice requests and handlers: `GetSettings` / `GetSettingsHandler`, `UpdateSettings` / `UpdateSettingsHandler`.

> **Convention:** To add new settings, create a class inheriting from `DynamicSettingsSection`, declare `SectionName`, and expose properties using `GetValue<T>(nameof(Property))`. The system automatically loads the file `settings.{SectionName}.json` with optional `settings.{SectionName}.{Environment}.json` overlay. For modules, place custom `DynamicSettingsSection` subclasses in the module assembly — they are auto-discovered and registered during `AddModule<T>()`.

**Example — creating a custom settings section:**
```csharp
public class PaymentSettings : DynamicSettingsSection
{
    public override string SectionName => "Payment";

    public string Gateway => GetValue<string>(nameof(Gateway));
    public decimal TaxRate => GetValue<decimal>(nameof(TaxRate));

    public PaymentSettings(IJsonSerializer jsonSerializer, string? environment = null)
        : base("settings.Payment.json", jsonSerializer, environment) { }
}
```
Corresponding file `settings.Payment.json`:
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
A channel-based message persistence and processing system (outbox pattern).

- **`Message`** — Entity with `Id`, `Type` (CLR type name), `Content` (serialized JSON), `Channel` (enum), `OccurredAt`, `ProcessedAt`, `ProcessingAt`, `RetryCount`, `NextRetryAt`, `Partition`, `LastError`.
- **`MessageChannel`** — Enum: `Event`, `Email`.
- **`IMessageWriter` / `MessageWriter`** — Serializes content, assigns a partition, persists to `IKernelDbContext.Messages`.
- **`IMessageProcessor`** — Per-channel processor: `GetChannel()`, `ProcessMessage()`.
- **`EventMessageProcessor`** — Processes `MessageChannel.Event` messages: resolves event type via `IEventTypeRegistry`, deserializes, publishes to `IEventBus` within a `TransactionScope`.
- **`IMessageDispatcher` / `MessageDispatcher`** — Routes messages to the correct `IMessageProcessor` by channel.

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

---

### 🔲 Stub / Not Yet Implemented

#### Health Checks (`Kernel/Health/`)
Both classes are empty stubs:
- `HealthReport` — Should aggregate health info per-module.
- `HealthReportComponent` — Should represent a single health check within a module.

> **Design intent:** Each module registers its own health checks. The health system groups and reports them by module.

---

## Configuration Architecture

The system uses **two layers** of configuration:

1. **ASP.NET `IConfiguration`** (`appsettings.json`) — static, host-level ASP.NET settings only (logging, allowed hosts). Bound via standard ASP.NET hosting.
2. **Dynamic Settings** (`settings.{Section}.json`) — runtime-mutable settings with environment overlays, auto-persist, and change events.

```
Host/
├── appsettings.json                           # ASP.NET IConfiguration: AllowedHosts, Logging
├── appsettings.{Environment}.json             # ASP.NET environment overrides (logging, etc.)
├── settings.Application.json                  # DynamicSettings: Environment, PartitionAmount
├── settings.Application.{Environment}.json    # Optional environment overlay
├── settings.Data.json                         # DynamicSettings: Provider, ConnectionString
├── settings.Data.{Environment}.json            # Optional environment overlay
├── settings.Instance.json                     # DynamicSettings: OpenApiViewer, Partition, ActiveModules
└── settings.Instance.{Environment}.json       # Optional environment overlay

Modules/
└── {ModuleName}/
    ├── settings.{ModuleName}.json             # Module-specific dynamic settings
    └── settings.{ModuleName}.{Environment}.json  # Optional environment overlay
```

### appsettings.json (ASP.NET static config only)
```json
{
  "AllowedHosts": "*"
}
```

### settings.Application.json (ApplicationSettings)
```json
{
  "Environment": "Development",
  "PartitionAmount": 100
}
```

### settings.Data.json (DataSettings)
```json
{
  "Provider": "Sqlite",
  "ConnectionString": "Data Source=kernel.db"
}
```

### settings.Instance.json (InstanceSettings)
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

### Environment Overlays
For any section, create `settings.{Section}.{Environment}.json` in the same directory. Values in the environment file override the base file. Writes always go to the base file.

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

Scaffolds a new module under `Modules/{ModuleName}/`. The template is a **class library** (not a web project) that references `Microsoft.AspNetCore.App`.

| File | Purpose |
|---|---|
| `{Name}Module.cs` | `IModule` implementation with `Name`, `Register()`, and `MapEndpoints()`. |
| `{Name}.csproj` | `Microsoft.NET.Sdk` library; references Kernel. Includes `settings.*.json` as `Content` with `CopyToOutputDirectory`. |
| `settings.{Name}.json` | Module-specific dynamic settings (base). |
| `settings.{Name}.Development.json` | Module-specific development environment overlay. |

**Module IModule implementation pattern:**
```csharp
public class MyModuleModule : IModule
{
    public static string Name => "MyModule";

    public static void Register(IServiceCollection services, IConfiguration configuration)
    {
        // Register services, DbContexts, etc.
    }

    public static void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/mymodule")
            .WithTags("MyModule")
            .WithGroupName(Name);
        // Map GET/POST/etc. here
    }
}
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
1. Executes `dotnet new mwa-module -n Orders`
2. Adds the project to the `.slnx` solution under the `Modules` solution folder (`dotnet sln add Modules/Orders/Orders.csproj -s Modules`)
3. Adds the project reference to `Host/Host.csproj` (`dotnet add Host/Host.csproj reference Modules/Orders/Orders.csproj`)

#### Manual Module Creation & Project References
Alternatively, to perform these steps manually:

1. **Scaffold the Module:**
   ```bash
   dotnet new mwa-module -n MyModule
   ```

2. **Add to Solution:**
   ```bash
   dotnet sln add Modules/MyModule/MyModule.csproj -s Modules
   ```

3. **Reference the Module in the Host (`Host/Host.csproj`):**
   ```xml
   <ItemGroup>
     <ProjectReference Include="..\Modules\MyModule\MyModule.csproj" />
   </ItemGroup>
   ```
   Or via CLI:
   ```bash
   dotnet add Host/Host.csproj reference Modules/MyModule/MyModule.csproj
   ```

4. **Enable the Module in `Host/settings.Instance.json`:**
   ```json
   {
     "ActiveModules": {
       "MyModule": true
     }
   }
   ```

---

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
Host → Kernel, Data
Data → Kernel
Module → Kernel (and Data if using EF DbContexts)
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
| New static host-level setting | `Host/appsettings.json` (ASP.NET static configuration, e.g. Logging, AllowedHosts) |
| New runtime-mutable setting | New `DynamicSettingsSection` subclass with `SectionName` + `settings.{SectionName}.json` file |
| New module | `Modules/{ModuleName}/` (use `modular-webapi-module` template) |
| Module endpoints | `IModule.MapEndpoints(IEndpointRouteBuilder)` inside the module project |
| Module-specific entities, handlers, services | Inside the module project |
| Host pipeline configuration (viewers, middleware) | `Host/Program.cs` |
| Global NuGet package settings | `Directory.Build.props` |
| Host settings (ASP.NET only: logging, allowed hosts) | `Host/appsettings.json` (ASP.NET `IConfiguration`) |
| Per-instance settings | `Host/settings.Instance.json` via `InstanceSettings` |
| Application-level mutable settings | `Host/settings.Application.json` via `ApplicationSettings` |
| Database / Data dynamic settings | `Host/settings.Data.json` via `DataSettings` |
| Module-specific settings | `Modules/{ModuleName}/settings.{ModuleName}.json` |
| Third-party licenses | `Kernel/ThirdParty/Licenses/` |
