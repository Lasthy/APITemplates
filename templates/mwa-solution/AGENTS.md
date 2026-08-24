# MyAPI — Agent Guide

This repository is a **Modular Web API** (MWA) monolith in ASP.NET (.NET 10). It organizes features into decoupled, pluggable modules sharing a common Kernel and Data layer.

---

## Architecture Overview

```
Solution Root (MyAPI.slnx)
├── Host/                    # ASP.NET Web App — entry point, pipeline, static appsettings
│   └── Program.cs           # builder.AddKernel() + builder.AddData() + builder.AddModules()
├── Data/                    # Shared EF Core database infrastructure & contexts
│   ├── Configuration/       # DataSettings (dynamic settings for Database: Provider, ConnectionString)
│   ├── Extensions/          # WebApplicationBuilder extension (AddData)
│   └── Persistence/         # KernelDbContext (implements IKernelDbContext)
├── Kernel/                  # Shared class library — all cross-cutting concerns
│   ├── Common/              # Result pattern (Result, Result<T>, PagedResult<T>)
│   ├── Configuration/       # Dynamic settings system (DynamicSettingsSection, DynamicSettings)
│   ├── Constants/           # Application-wide constants (Roles)
│   ├── Context/             # Scoped operation context (IScopeContext, WebScopeContext)
│   ├── Entities/            # Entity marker interfaces (IAuditable, ISoftDeletable, IHasDomainEvents)
│   ├── Events/              # EventBus abstraction + in-process impl + registry
│   ├── Extensions/          # WebApplicationBuilder extension (AddKernel, UseKernel)
│   ├── Health/              # Health check models
│   ├── Json/                # Centralized JSON serializer abstraction (IJsonSerializer)
│   ├── Messaging/           # Outbox/Inbox message persistence + processing (Kernel.Boxing)
│   ├── Modules/             # IModule contract + module registration infrastructure
│   ├── Partitioning/        # Partition management for multi-instance deployments
│   ├── Persistence/         # IDbContext, IKernelDbContext + EF SaveChanges interceptors
│   ├── Requests/            # Request/Response + Dispatcher + Interceptor interfaces
│   ├── Ulid/                # Vendored Ulid implementation + EF/JSON/Type converters
│   ├── Workers/             # BaseWorker background service (periodic / one-shot)
│   └── ThirdParty/          # Third-party license files
├── Modules/                 # Directory where module projects live
│   └── {ModuleName}/        # Pluggable module project (e.g. Catalog, Orders)
├── create-module.ps1        # Helper script to scaffold, sln-add, and Host-reference a new module (PowerShell)
├── create-module.sh         # Helper script to scaffold, sln-add, and Host-reference a new module (Bash)
├── GlobalUsings.Shared.cs   # `global using DefaultId = System.Ulid;`
└── Directory.Build.props    # Auto-includes GlobalUsings.Shared.cs in all projects
```

### Key Design Principles

- **Kernel owns all cross-cutting concerns.** Modules depend on Kernel; Kernel never depends on modules.
- **Modules are self-contained projects.** Each module has its own `settings.{ModuleName}.json` and implements `IModule` for DI and endpoint registration.
- **Module activation is config-driven.** The `InstanceSettings` section in `settings.Instance.json` has an `ActiveModules` dictionary (`{ "ModuleName": true/false }`) controlling which modules are loaded at startup.
- **`DefaultId` is `System.Ulid` everywhere.** Defined in `GlobalUsings.Shared.cs` and propagated to all projects via `Directory.Build.props`.

---

## Project Breakdown

### Host (`Host/`)

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
| `settings.Messaging.json` | Dynamic settings for outbox messaging: processing interval, batch size, timeout, retry policy, cleanup retention. |

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

### Data (`Data/`)

A shared class library for Entity Framework Core persistence implementations, database provider registration, and shared DbContext infrastructure.

- **`KernelDbContext`** — Concrete EF Core DbContext implementing `IKernelDbContext` (owns `DbSet<Message> Messages`).
- **`DataSettings`** — Dynamic settings for database provider and connection string (`settings.Data.json`).
- **`WebApplicationBuilderExtensions.AddData()`** — Registers `DataSettings`, wires up DbContext registration, and registers `IKernelDbContext` in DI.

### Kernel (`Kernel/`)

A class library (not a web project) that provides all shared infrastructure. It targets `net10.0` and references `Microsoft.AspNetCore.App`, Entity Framework Core, and OpenAPI packages.

---

## Core Features & Conventions

### Standardized Result (`Kernel/Common/`)
Railway-oriented result pattern for operation outcomes.

- **`Result`** — Base record: `IsSuccess`, `IsFailure`, `Messages[]`, `Error` (Exception, `[JsonIgnore]`). Factory methods: `Success()`, `Failure(Exception)`, `Success<T>(value)`, `Failure<T>(error)`. Has `Match<TOut>()`.
- **`Result<T>`** — Generic variant with `Value`. Supports `Map()`, `Bind()`, `Match()`.
- **`PagedResult<T>`** — Extends `Result<IEnumerable<T>>` with `Page` and `PageSize`.
- **`ResultExtensions`** — Async extension methods: `MapAsync`, `BindAsync`, `MatchAsync`.

> **Convention:** All operations should return `Result` or `Result<T>`. Never throw exceptions for expected failures — wrap them in `Result.Failure()`.

### Dynamic Configuration (`Kernel/Configuration/`)
A runtime-mutable, convention-based settings system that reads from JSON files, supports environment overlays, auto-persists changes, and raises events on mutation.

**File naming convention:** `settings.{SectionName}.json` (base) with optional `settings.{SectionName}.{Environment}.json` (environment overlay).

- **`DynamicSettingsSection`** — Abstract base class. Subclasses declare `SectionName` and expose strongly-typed properties via `GetValue<T>(key)` / `SetValue(key, value)`. `SetValue()` auto-persists to the source file and raises `OnSettingsChanged`.
- **`DynamicSettings`** — Singleton registry of `DynamicSettingsSection` instances.
- **`SettingsChangedEvent`** — `record(SectionName, Key) : Event`. Published via `IEventBus` whenever any section value changes.

### Scope Context (`Kernel/Context/`)
Standardized context for the current operation scope, providing authentication and identity details.

- **`IScopeContext`** — Interface: `UserId` (`DefaultId?`), `IsAuthenticated`, `Roles`, `Claims`, `GetClaim()`, `GetClaimValues()`, `IsInRole()`, `IsSystem`.
- **`WebScopeContext`** — Implementation backed by `IHttpContextAccessor`.
- **`Roles.System`** — Constant (`"System"`). Super-user role with unrestricted access.

### Background Workers (`Kernel/Workers/`)
- **`BaseWorker`** — Abstract `BackgroundService` with:
  - Configurable `Interval` (default 1 min)
  - Configurable `IsOneShot` mode (runs once and finishes, useful for startup tasks or data migration)
  - Creates a DI scope per iteration (or once if one-shot)
  - Calls `ExecuteJobAsync(IServiceProvider services, CancellationToken ct)`
  - Error handling with structured logging
  - Supports `RequestCancellation()` for graceful self-stop.

### Messaging & Outbox (`Kernel/Messaging/`)
Channel-based outbox message persistence, partition-sharded background processing, fault tolerance, and retention management:

- **`Message`** — Entity storing serialized payload, CLR type, channel, partition, and lifecycle timestamps (`OccurredAt`, `ProcessingAt`, `ProcessedAt`, `NextRetryAt`, `RetryCount`, `LastError`).
- **`IMessageWriter` / `MessageWriter`** — Serializes content, assigns a random partition via `IPartitionManager`, and persists to `IKernelDbContext.Messages`.
- **`IMessageProcessor`** — Per-channel processing contract (`GetChannel()`, `ProcessMessage()`). Default implementation: `EventMessageProcessor` for `MessageChannel.Event`.
- **`IMessageDispatcher` / `MessageDispatcher`** — Routes messages by channel to their corresponding processor, returning `Task<Result>`.
- **`MessageProcessingWorker`** — Hosted `BaseWorker` running on `ProcessingIntervalSeconds`:
  - **Partitioning**: Only processes messages matching `instanceSettings.Partition` (`[Start..End]`).
  - **Stuck/Timeout Recovery**: Detects messages in `ProcessingAt` exceeding `ProcessingTimeoutSeconds` and marks them timed out for retry.
  - **Exponential Backoff**: Reschedules failed messages with exponential delay ($2^{\text{retry}-1} \times \text{InitialDelay}$, capped at `MaxRetryDelaySeconds`) until `MaxRetryAttempts`.
- **`MessageCleanupWorker`** — Hosted `BaseWorker` running on `CleanupIntervalHours`, deleting processed messages older than `CleanupRetentionDays` (and optionally permanently failed messages).
- **`MessagingSettings`** — Dynamic settings loaded from `settings.Messaging.json`.

### Request/Response & Interceptors (`Kernel/Requests/`)
MediatR-style CQRS pattern with attribute-based interceptors:

- **`IRequest<TResponse>`** — Marker interface for requests returning `Result<TResponse>`.
- **`IRequestHandler<TRequest, TResponse>`** — Handler interface: `HandleAsync(request, ct)`.
- **`IDispatcher` / `Dispatcher`** — Dispatches requests through interceptor pipelines to their handlers.
- **`[Logging]`** — Interceptor attribute for logging payload, execution duration, and outcome.
- **`[Transaction]`** — Interceptor attribute wrapping execution in a `TransactionScope`.
- **`[Intercept<T>]` / `[Intercept(typeof(T))]`** — Attaches custom `IRequestInterceptor` implementations.
- **`Order`** — Control pipeline execution order (lower numbers run outermost, e.g. `[Logging(Order = 1)]`, `[Transaction(Order = 2)]`).

### Persistence & EF Core Interceptors (`Kernel/Persistence/`)
Shared database infrastructure and automated cross-cutting concerns:

- **`IDbContext`** / **`IKernelDbContext`** — Database context contracts for EF Core operations and outbox messages.
- **`AuditSaveChangesInterceptor`** — Automatically sets `CreatedAt`/`CreatedBy` on entity addition and `UpdatedAt`/`UpdatedBy` on entity modification using `IScopeContext`.
- **`SoftDeleteSaveChangesInterceptor`** — Converts hard deletes on `ISoftDeletable` entities into soft deletes (`EntityState.Modified`), updating `DeletedAt` and `DeletedBy`.
- **`DispatchDomainEventsSaveChangesInterceptor`** — Automatically collects and dispatches `DomainEvents` from `IHasDomainEvents` entities through `IEventBus` upon saving and clears them.
- **`DbContextOptionsBuilderExtensions`** — Helper extension methods (`UseConfiguredDatabase`, `AddKernelInterceptors`) for configuring providers and attaching interceptors.

### Module System (`Kernel/Modules/`)
Pluggable module architecture:

- **`IModule`** — Contract:
  - `static abstract string Name` — module identifier; must match the key in `ActiveModules`.
  - `static abstract void Register(IServiceCollection, IConfiguration)` — DI registration.
  - `static abstract void MapEndpoints(IEndpointRouteBuilder)` — route registration.
- **`AddModules()`** / **`UseModules()`** — Discovers active modules from `settings.Instance.json`, registers dynamic settings, scans and registers event handlers (`IEventHandler<>`), request handlers (`IRequestHandler<,>`), and health checks (`IHealthCheck`), registers OpenAPI docs, and maps endpoints.

### Health Checks (`Kernel/Health/`)
Modular health report system grouped by module:

- **`IHealthCheck`** — Interface: `Name`, `CheckHealthAsync(ct)` returning `HealthCheckResult` (`Healthy`, `Degraded`, `Unhealthy`).
- **`HealthReport`** & **`ModuleHealthReport`** & **`HealthReportComponent`** — Hierarchical health aggregation.
- **`IHealthCheckRegistry`** & **`IHealthService`** — Auto-discovers module health checks, executes checks, and measures duration.
- **Endpoints**:
  - `GET api/v1/kernel/health/live` (`[AllowAnonymous]`) — Liveness probe (200 OK).
  - `GET api/v1/kernel/health/ready` (`[AllowAnonymous]`) — Readiness probe (200 OK or 503).
  - `GET api/v1/kernel/health/report` (`[Authorize(Roles = Roles.System)]`) — Full system health report.
  - `GET api/v1/kernel/health/report/{moduleName}` (`[Authorize(Roles = Roles.System)]`) — Module health report.

### Module Scaffolding
Use the helper scripts from the solution root:
```powershell
# PowerShell
./create-module.ps1 Orders
```
or
```bash
# Bash
./create-module.sh Orders
```

This automatically scaffolds the module, adds it to the solution under the `Modules` folder, and references it in `Host/Host.csproj`.

---

## Coding Conventions

### Naming & Structure
- **Identifiers**: Always `DefaultId` (= `System.Ulid`). Never use `Guid`, `int`, or `string` for entity IDs.
- **Features & Requests**: Place requests/commands/queries in `Features/`. Do **not** append `Request` to the class/record name; name them directly as verbs/actions (e.g., `GetOrders`, `CreateOrder`). The handler is named with a `Handler` suffix (e.g. `GetOrdersHandler`, `CreateOrderHandler`).
- **Endpoints & Routing**: All API endpoints must start with `api/<version>/` (e.g., `api/v1/orders`). Group endpoints using `.WithTags("{ModuleName}").WithGroupName("{ModuleName}")`.
- **Dependencies**: Modules depend on `Kernel` (and `Data` if using EF). Modules should never depend on other modules directly — use domain events or Kernel messaging instead.
