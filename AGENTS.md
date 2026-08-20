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
│   └── Program.cs           # builder.AddKernel() + builder.AddModules()
├── Kernel/                  # Shared class library — all cross-cutting concerns
│   ├── Common/              # Result pattern
│   ├── Configuration/       # Dynamic settings system
│   ├── Context/             # Scoped operation context (auth, claims)
│   ├── Entities/            # Entity marker interfaces (audit, soft-delete, domain events)
│   ├── Events/              # EventBus abstraction + in-process impl + registry
│   ├── Extensions/          # WebApplicationBuilder extension (AddKernel)
│   ├── Health/              # Health check models (stub)
│   ├── Json/                # Centralized JSON serializer abstraction
│   ├── Messaging/           # Outbox/Inbox message persistence + processing
│   ├── Modules/             # IModule contract + module registration infrastructure
│   ├── Partitioning/        # Partition management for multi-instance deployments
│   ├── Persistence/         # KernelDbContext + EF SaveChanges interceptors
│   ├── Requests/            # Request/Response + Dispatcher + Interceptor interfaces (stub)
│   ├── Ulid/                # Vendored Ulid implementation + EF/JSON/Type converters
│   ├── Workers/             # BaseWorker background service
│   └── ThirdParty/          # Third-party license files
├── Modules/                 # Directory where module projects live (.gitkeep placeholder)
├── GlobalUsings.Shared.cs   # `global using DefaultId = System.Ulid;`
└── Directory.Build.props    # Auto-includes GlobalUsings.Shared.cs in all projects
```

### Key Design Principles

- **Kernel owns all cross-cutting concerns.** Modules depend on Kernel; Kernel never depends on modules.
- **Modules are self-contained projects.** Each module has its own `appsettings.json` and implements `IModule` for DI registration.
- **Module activation is config-driven.** The `InstanceSettings` section in the host's `appsettings.Instance.json` has an `ActiveModules` dictionary (`{ "ModuleName": true/false }`) controlling which modules are loaded at startup.
- **`DefaultId` is `System.Ulid` everywhere.** Defined in `GlobalUsings.Shared.cs` and propagated to all projects via `Directory.Build.props`.

---

## Project Breakdown

### Host (`templates/mwa-solution/Host/`)

The executable ASP.NET web application. This is the composition root.

| File | Purpose |
|---|---|
| `Program.cs` | Calls `builder.AddKernel()`, `builder.AddModules()`, `app.UseKernel()`, `app.UseModules()`, and mounts the OpenAPI viewer UI. |
| `Host.csproj` | Web SDK project, references Kernel. Includes `Scalar.AspNetCore` and `Swashbuckle.AspNetCore`. |
| `appsettings.json` | Main application settings — host, kernel, and `Api` section (OpenAPI viewer choice). |
| `appsettings.{Environment}.json` | Environment-specific overrides. |
| `appsettings.Instance.json` | Per-instance: partition range, active modules dictionary. |

**Program.cs composition pattern:**
```csharp
builder.AddKernel();      // Register kernel services + kernel OpenAPI doc
builder.AddModules();     // Discover + register active modules
var app = builder.Build();
app.UseKernel();          // Map /openapi/kernel.json
app.UseModules();         // Map module endpoints + /openapi/{module}.json + event types
// Mount Scalar or Swagger UI (reads Api.OpenApiViewer from appsettings.json)
```

**Configuration strategy:**
- The **main `appsettings.json`** contains host-level settings: logging, allowed hosts, `Api.OpenApiViewer`.
- An **`appsettings.Instance.json`** (read by `InstanceSettings`) contains per-instance config: partition range, active modules.
- Each **module has its own `appsettings.json`** for module-specific configuration.

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
A runtime-mutable settings system that reads from JSON files and can write changes back.

- **`DynamicSettingsSection`** — Reads a JSON file into a `Dictionary<string, JsonElement>`, exposes `GetValue<T>(key)` / `SetValue(key, value)`. Tracks `SettingsChanged`. Has `IsReadonly` guard and `Source` (file path).
- **`DynamicSettings`** — Registry of named `DynamicSettingsSection` instances. `CreateSection(source, name)`, `AddSection<T>(instance)`, `GetSection<T>()`. `CommitChanges()` persists all dirty sections back to their source files.
- **`AppSettings`** — Section exposing `Environment` and `PartitionAmount`.
- **`InstanceSettings`** — Section exposing `Partition` (start/end range) and `ActiveModules` dictionary.

> **Convention:** When adding new global settings, extend `AppSettings`. For per-instance settings (partition, modules), extend `InstanceSettings`. Module-specific settings should be their own `DynamicSettingsSection` subclass pointing at the module's `appsettings.json`.

> **WIP:** Change events are not yet implemented — the system needs to raise events when settings are modified so consumers can react.

#### Scope Context (`Kernel/Context/`)
Standardized context for the current operation scope, providing authentication information.

- **`IScopeContext`** — Interface: `UserId` (`DefaultId?`), `IsAuthenticated`, `Roles`, `Claims`, `GetClaim()`, `GetClaimValues()`, `IsInRole()`, `IsSystem`.
- **`WebScopeContext`** — Implementation backed by `IHttpContextAccessor`. Extracts user ID from `NameIdentifier` or `sub` claim, parses to `DefaultId`. Caches roles and claims. Has a protected constructor accepting `ClaimsPrincipal` for derived contexts. Implements `IsSystem => IsInRole(Roles.System)`.
- **`Roles`** — Static class with string role constants. `Roles.System = "System"` is the super-user role with unrestricted access.

> **Convention:** Inject `IScopeContext` into handlers and services. For non-web contexts (workers, event handlers), create a derived context.

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
  - `AddModule<T>()` — Calls `T.Register()`, registers named OpenAPI document (`builder.Services.AddOpenApi(T.Name)`), scans event handlers, records in `ModuleRegistry`.
  - `UseModules(WebApplication app)` — Post-build: iterates `ModuleRegistry`, calls `IEventTypeRegistry.RegisterFromAssembly()` per module, calls `module.MapEndpoints(app)`, maps `/openapi/{module.Name}.json`.

> **Convention:** Each module project must have exactly one class implementing `IModule`. The class name doesn't matter; it's discovered by interface. The module assembly name must match the key in `ActiveModules`.

#### Messaging / Outbox (`Kernel/Messaging/`)
A channel-based message persistence and processing system (outbox pattern).

- **`Message`** — Entity with `Id`, `Type` (CLR type name), `Content` (serialized JSON), `Channel` (enum), `OccurredAt`, `ProcessedAt`, `ProcessingAt`, `RetryCount`, `NextRetryAt`, `Partition`, `LastError`.
- **`MessageChannel`** — Enum: `Event`, `Email`.
- **`IMessageWriter` / `MessageWriter`** — Serializes content, assigns a partition, persists to `KernelDbContext.Messages`.
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
- **`BaseWorker`** — Abstract `BackgroundService` with a configurable `Interval` (default 1 min). Creates a DI scope per iteration, calls `ExecuteJobAsync()`, handles errors with structured logging. Supports `RequestCancellation()` for graceful self-stop.

> **Convention:** Module workers inherit from `BaseWorker`. Override `ExecuteJobAsync(IServiceProvider, CancellationToken)`. Resolve scoped services from the provided `IServiceProvider`.

#### Partitioning (`Kernel/Partitioning/`)
For distributing work across multiple API instances.

- **`IPartitionManager`** — `GetRandomPartition()`.
- **`PartitionManager`** — Depends on `InstanceSettings`. Returns a random `int` in `[Partition.Start, Partition.End]` using `Random.Shared`.

> **Convention:** Use `IPartitionManager.GetRandomPartition()` when writing outbox messages to assign a partition. Each instance owns a range of partition numbers configured in `appsettings.Instance.json`.

#### API Settings (`Kernel/Configuration/`)
Static (non-mutable) host-level configuration bound from `IConfiguration`.

- **`ApiSettings`** — POCO bound from the `Api` section of `appsettings.json`. Properties: `OpenApiViewer` (`"scalar"` | `"swagger"`, default `"scalar"`).

> **Convention:** Add host-level static settings to `ApiSettings`. For runtime-mutable settings, use `DynamicSettingsSection` subclasses. `ApiSettings` is registered as a singleton by `AddKernel()`.

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

#### EF Core Persistence (`Kernel/Persistence/`)
- **`KernelDbContext`** — Owns `DbSet<Message> Messages`. This is the Kernel's own database context for infrastructure entities (messages/outbox).
- **`AuditSaveChangesInterceptor`** — Stub class; should intercept `SaveChanges` to auto-populate `IAuditable` fields.
- **`SoftDeleteSaveChangesInterceptor`** — Stub class; should intercept `SaveChanges` to convert deletes into soft-deletes for `ISoftDeletable` entities.

---

### 🔲 Stub / Not Yet Implemented

#### Request/Response Pattern (`Kernel/Requests/`)
All interfaces and the dispatcher are empty stubs:
- `IRequest` — Should define a request that returns `Result<TResponse>`.
- `IRequestHandler` — Should handle a request and produce a response.
- `IRequestInterceptor` — Should wrap handler execution (logging, validation, auth, etc.).
- `IDispatcher` / `Dispatcher` — Should resolve and invoke the correct handler with the interceptor pipeline.

> **Design intent:** This is a MediatR-like pattern. A request goes through interceptors → handler → result. Interceptors form a pipeline (think: validation interceptor, logging interceptor, transaction interceptor).

#### Health Checks (`Kernel/Health/`)
Both classes are empty stubs:
- `HealthReport` — Should aggregate health info per-module.
- `HealthReportComponent` — Should represent a single health check within a module.

> **Design intent:** Each module registers its own health checks. The health system groups and reports them by module.

#### Data Project (Planned)
A separate `Data` project to centralize EF database concerns. This would be a shared project that modules reference for common EF infrastructure (base DbContext utilities, common interceptors, migration helpers, etc.).

---

## Configuration Architecture

```
Host/
├── appsettings.json                    # Shared: AllowedHosts, Api.OpenApiViewer
├── appsettings.{Environment}.json      # Environment overrides (logging, etc.)
└── appsettings.Instance.json           # Per-instance: partition range, active modules

Modules/
└── {ModuleName}/
    └── appsettings.json                # Module-specific settings
```

### appsettings.json (Api section)
```json
{
  "AllowedHosts": "*",
  "Api": {
    "OpenApiViewer": "scalar"
  }
}
```
Change `OpenApiViewer` to `"swagger"` to use Swagger UI instead of Scalar.

### appsettings.Instance.json (expected shape)
```json
{
  "Partition": { "Start": 0, "End": 99 },
  "ActiveModules": {
    "MyModule": true,
    "AnotherModule": false
  }
}
```

### OpenAPI Documents
Each active component gets its own named OpenAPI document:
- `kernel` → `/openapi/kernel.json` — registered by `AddKernel()`
- `{ModuleName}` → `/openapi/{ModuleName}.json` — registered by `AddModule<T>()`

All documents are exposed in the viewer UI (Scalar or Swagger) mounted at `/scalar` or `/docs`.

### Module appsettings.json
Each module should own its own settings file. Module settings are loaded as a `DynamicSettingsSection` pointing at the module's `appsettings.json`.

---

## Module Template (`templates/mwa-module/`)

Scaffolds a new module under `Modules/{ModuleName}/`. The template is a **class library** (not a web project) that references `Microsoft.AspNetCore.App`.

| File | Purpose |
|---|---|
| `{Name}Module.cs` | `IModule` implementation with `Name`, `Register()`, and `MapEndpoints()`. |
| `{Name}.csproj` | `Microsoft.NET.Sdk` library; add `ProjectReference` to Kernel. |
| `appsettings.json` | Module-specific settings placeholder. |

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

> **To activate a module**: add its assembly to `ActiveModules` in `appsettings.Instance.json` and add a `ProjectReference` + `<Compile>` in the solution's `Directory.Build.props` or directly in `Host.csproj`.

---

## System Role

The `Roles.System` constant (`"System"`) represents a super-user role with unrestricted access to all operations, including kernel-level administrative endpoints.

### Files
- **`Kernel/Context/Roles.cs`** — `public static class Roles { public const string System = "System"; }`
- **`IScopeContext.IsSystem`** — Shorthand for `IsInRole(Roles.System)`
- **`WebScopeContext.IsSystem`** — Implemented as `IsInRole(Context.Roles.System)`

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
- **Extension methods** for `WebApplicationBuilder` go in files named `WebApplicationBuilderExtensions.cs` within their feature folder.

### Patterns
- **Result pattern** for all operation returns — no throwing expected exceptions.
- **Interface-first** — features expose an interface (e.g. `IEventBus`, `IJsonSerializer`, `IPartitionManager`) with a default implementation. Consumers depend on the interface.
- **Convention-based DI registration** — Module registration auto-scans assemblies for `IEventHandler<>` implementations.

### Project Dependencies
```
Host → Kernel
Module → Kernel (and optionally a future Data project)
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
| New entity marker interface | `Kernel/Entities/` |
| New EF interceptor | `Kernel/Persistence/` |
| New message channel + processor | `Kernel/Messaging/` (namespace: `Kernel.Boxing`) |
| New global type alias | `GlobalUsings.Shared.cs` |
| New kernel DI registration | `Kernel/Extensions/WebApplicationBuilderExtensions.cs` (`AddKernel`) |
| New kernel pipeline step | `Kernel/Extensions/WebApplicationBuilderExtensions.cs` (`UseKernel`) |
| New kernel extension method (feature-local) | Feature-local `WebApplicationBuilderExtensions.cs` |
| New role constant | `Kernel/Context/Roles.cs` |
| New static host-level setting | `Kernel/Configuration/ApiSettings.cs` (bound from `appsettings.json`) |
| New runtime-mutable setting | New `DynamicSettingsSection` subclass pointing at the relevant JSON file |
| New module | `Modules/{ModuleName}/` (use `modular-webapi-module` template) |
| Module endpoints | `IModule.MapEndpoints(IEndpointRouteBuilder)` inside the module project |
| Module-specific entities, handlers, services | Inside the module project |
| Host pipeline configuration (viewers, middleware) | `Host/Program.cs` |
| Global NuGet package settings | `Directory.Build.props` |
| Host settings (logging, allowed hosts, Api section) | `Host/appsettings.json` |
| Per-instance settings | `Host/appsettings.Instance.json` via `InstanceSettings` |
| Module-specific settings | `Modules/{ModuleName}/appsettings.json` |
| Third-party licenses | `Kernel/ThirdParty/Licenses/` |
