# APITemplates — Agent Guide

This repository contains `dotnet new` templates for building **Modular Web API** (MWA) monoliths in ASP.NET (.NET 10). It ships two templates that work together:

| Template | Short Name | Type | Path |
|---|---|---|---|
| **Modular API Solution** | `modular-webapi` | Solution | `templates/mwa-solution/` |
| **Modular API Module** | `modular-webapi-module` | Project | `templates/mwa-module/` |
| **Modular API Identity Module** | `mwa-module-identity` | Project | `templates/mwa-module-identity/` |
| **Modular API Multi-Tenancy Module** | `mwa-module-multitenancy` | Project | `templates/mwa-module-multitenancy/` |
| **Modular API Scheduling Module** | `mwa-module-scheduling` | Project | `templates/mwa-module-scheduling/` |

The solution template scaffolds the **Host** (entry-point web app) and the **Kernel** (shared library). The module templates scaffold module projects that plug into the solution (`mwa-module` for generic features, `mwa-module-identity` for identity/auth, `mwa-module-multitenancy` for multi-tenancy, `mwa-module-scheduling` for job scheduling).

---

## Architecture Overview

```
Solution Root (MyAPI.slnx)
├── Host/                    # ASP.NET Web App — entry point, pipeline, appsettings
│   └── Program.cs           # builder.AddKernel() + builder.AddData() + builder.AddModules()
├── Kernel/                  # Shared class library — all cross-cutting concerns & persistence
│   ├── AuditLog/            # Structured audit logging (AuditEntry, SaveChanges interceptor, service, controller)
│   ├── Authorization/       # Policy-based authorization (IAuthorizationPolicy, registry, interceptor, filters)
│   ├── Caching/             # Caching abstraction (ICacheService, InMemoryCacheService, CacheAttribute, CacheInterceptor, CacheSettings)
│   ├── Common/              # Result pattern
│   ├── Configuration/       # Dynamic settings system (Controllers/, Features/, DataSettings)
│   ├── Constants/           # Application-wide constants (Roles)
│   ├── Context/             # Scoped operation context (auth, claims)
│   ├── Diagnostics/         # Observability & diagnostics (CorrelationId, RequestDiagnostics, ActivitySource)
│   ├── Email/               # Email delivery abstraction (IEmailSender, SmtpEmailSender, EmailMessage, EmailSettings, template renderer)
│   ├── Encryption/          # Column-level data encryption at rest (IEncryptionService, AesEncryptionService, [Encrypted], EncryptedValueConverter, EncryptionSettings)
│   ├── Entities/            # Entity marker interfaces (audit, soft-delete, domain events)
│   ├── Events/              # EventBus abstraction + in-process impl + registry
│   ├── Extensions/          # WebApplicationBuilder extensions (AddKernel, AddData, UseKernel, UseData)
│   ├── Health/              # Health check models + checks (MemoryHealthCheck, DatabaseHealthCheck)
│   ├── Json/                # Centralized JSON serializer abstraction
│   ├── Messaging/           # Outbox/Inbox message persistence + processing
│   ├── Modules/             # IModule contract + module registration infrastructure
│   ├── Notifications/       # Real-time notifications abstraction (INotificationService, SignalRNotificationService, NotificationHub, NotificationSettings, extensions)
│   ├── Partitioning/        # Partition management for multi-instance deployments
│   ├── Persistence/         # IDbContext, IDbContextSchema, KernelDbContext, DbContextTypeRegistry + SaveChanges interceptors
│   ├── RateLimiting/        # Dynamic, partition-based rate limiting (RateLimitingSettings, extensions)
│   ├── Requests/            # Request/Response + Dispatcher + Interceptor interfaces (stub)
│   ├── Storage/             # File storage abstraction (IFileStorage, LocalFileStorage, FileStorageSettings, extensions)
│   ├── Ulid/                # Vendored Ulid implementation + EF/JSON/Type converters
│   ├── Versioning/          # URL/header/query API versioning (VersioningSettings, VersioningExtensions)
│   ├── Workers/             # BaseWorker background service
│   └── ThirdParty/          # Third-party license files
├── Kernel.Testing/          # Testing companion library (fakes, base test fixtures, fluent assertions)
│   ├── Assertions/          # Fluent assertions (ResultAssertions, EventBusAssertions, EmailSenderAssertions, CacheAssertions, EncryptionAssertions, ApiVersioningAssertions, FileStorageAssertions, NotificationAssertions)
│   ├── Fakes/               # Test fakes (FakeEventBus, FakeScopeContext, FakeEmailSender, FakeCacheService, FakeEncryptionService, FakeFileStorage, FakeNotificationService)
│   └── Infrastructure/      # Base classes & web host factory (KernelTestBase, ModuleTestBase, TestWebApplicationFactory)
├── Modules/                 # Directory where module projects live
│   ├── {ModuleName}/        # Module implementation (domain logic, handlers, endpoints, DbContext)
│   └── {ModuleName}.Contracts/ # Companion public contracts (events, requests, DTOs)
├── create-module.ps1        # Helper script to scaffold, sln-add, and Host-reference a new module (PowerShell)
├── create-module.sh         # Helper script to scaffold, sln-add, and Host-reference a new module (Bash)
├── GlobalUsings.Shared.cs   # `global using DefaultId = System.Ulid;`
└── Directory.Build.props    # Auto-includes GlobalUsings.Shared.cs in all projects
```

### Key Design Principles

- **Kernel owns all cross-cutting concerns and persistence infrastructure.** Modules depend on Kernel; Kernel never depends on modules.
- **Modules are self-contained projects.** Each module has its own `settings.{ModuleName}.json`, its own EF Core `DbContext` (with schema isolation), and implements `IModule` for DI registration.
- **Companion Contract Projects for Cross-Module Communication.** Modules expose public contracts (events, requests, DTOs) via a companion `{ModuleName}.Contracts` project. A module may reference another module's `.Contracts` project, but **never** its implementation project.
- **Module activation is config-driven.** The `InstanceSettings` section in `settings.Instance.json` has an `ActiveModules` dictionary (`{ "ModuleName": true/false }`) controlling which modules are loaded at startup.
- **`DefaultId` is `System.Ulid` everywhere.** Defined in `GlobalUsings.Shared.cs` and propagated to all projects via `Directory.Build.props`.

---

## Project Breakdown

### Host (`templates/mwa-solution/Host/`)

The executable ASP.NET web application. This is the composition root.

| File | Purpose |
|---|---|
| `Program.cs` | Calls `builder.AddKernel()`, `builder.AddData()`, `builder.AddModules()`, `app.UseKernel()`, `app.UseData()`, `app.UseModules()`, and mounts the OpenAPI viewer UI. |
| `Host.csproj` | Web SDK project, references Kernel. Includes `Scalar.AspNetCore` and `Swashbuckle.AspNetCore`. |
| `appsettings.json` | Root dynamic settings — logging, allowed hosts (backed by `HostSettings`, feeds into `IConfiguration`). |
| `appsettings.{Environment}.json` | Environment-specific overrides for root dynamic settings (logging levels, etc.). |
| `appsettings.Application.json` | Dynamic settings for the application: `Environment`, `PartitionAmount`. |
| `appsettings.Application.{Environment}.json` | Optional environment overlay for application settings. |
| `appsettings.Data.json` | Dynamic settings for data/database: `Provider`, `ConnectionString`. |
| `appsettings.Data.{Environment}.json` | Optional environment overlay for database settings. |
| `appsettings.Instance.json` | Per-instance dynamic settings: OpenAPI viewer choice, partition range, active modules dictionary. |
| `appsettings.Messaging.json` | Dynamic settings for outbox messaging: processing interval, batch size, timeout, retry policy, cleanup retention. |
| `appsettings.Email.json` | Dynamic settings for email delivery: SMTP host, port, credentials, SSL, From address/name. |
| `appsettings.RateLimiting.json` | Dynamic settings for rate limiting: global policy, authenticated policy, endpoint overrides, rejection response. |
| `appsettings.Cache.json` | Dynamic settings for caching: enabled flag, default expiration seconds, global key prefix. |
| `appsettings.Encryption.json` | Dynamic settings for data encryption: 256-bit AES key (Base64 or passphrase). |
| `appsettings.Versioning.json` | Dynamic settings for API versioning: default version, headers, query param, url segment flags. |
| `appsettings.Storage.json` | Dynamic settings for file storage: provider, base path, base URL, max size, allowed extensions. |
| `appsettings.Notifications.json` | Dynamic settings for real-time notifications: enabled flag, hub route path. |
| `appsettings.Spa.json` | Dynamic settings for Single Page Application (SPA) serving: dev server proxying, static files, and multi-SPA configuration. |

**Program.cs composition pattern:**
```csharp
builder.AddKernel();      // Register kernel services + kernel OpenAPI doc + dynamic settings in IConfiguration
builder.AddData();        // Register database options and DbContexts
builder.AddModules();     // Discover + register active modules
var app = builder.Build();
app.UseKernel();          // Map /openapi/kernel.json
app.UseData();            // Ensure databases and tables are created automatically on startup
app.UseModules();         // Map module endpoints + /openapi/{module}.json + event types
// Mount Scalar or Swagger UI (reads OpenApiViewer from appsettings.Instance.json)
app.UseOpenApiViewer();
app.UseHttpsRedirection();
app.UseSpa();             // Serve SPAs: reverse proxy in Development, static files & fallback in Production
```

**Configuration strategy:**
- **`appsettings.json`** (read by `HostSettings`) contains root host-level settings: logging, allowed hosts, and feeds directly into ASP.NET's `IConfiguration`.
- **`appsettings.Application.json`** (read by `ApplicationSettings`) contains runtime-mutable application settings.
- **`appsettings.Data.json`** (read by `DataSettings`) contains runtime-mutable database provider and connection string settings.
- **`appsettings.Instance.json`** (read by `InstanceSettings`) contains per-instance config: OpenAPI viewer choice (`OpenApiViewer`), partition range, active modules.
- Each **module has its own `appsettings.{ModuleName}.json`** with optional `appsettings.{ModuleName}.{Environment}.json` overlays for module-specific configuration.
- **All Dynamic Settings sections** automatically integrate into `builder.Configuration` (`IConfiguration`) and reload on changes.

### Kernel (`templates/mwa-solution/Kernel/`)

A class library (not a web project) that provides all shared infrastructure, persistence abstractions, database providers, and cross-cutting concerns. It targets `net10.0` and references `Microsoft.AspNetCore.App`, Entity Framework Core (with providers: SqlServer, PostgreSQL, MySql/MariaDb, Sqlite, InMemory), and OpenAPI packages.

---

## Feature Inventory & Status

### ✅ Implemented

#### Standardized Result & Pagination (`Kernel/Common/`)
A railway-oriented result pattern for operation outcomes and standardized query pagination and sorting.

- **`Result`** — Base record: `IsSuccess`, `IsFailure`, `Messages[]`, `Error` (Exception, `[JsonIgnore]`). Factory methods: `Success()`, `Failure(Exception)`, `Success<T>(value)`, `Failure<T>(error)`. Has `Match<TOut>()`.
- **`Result<T>`** — Generic variant with `Value`. Supports `Map()`, `Bind()`, `Match()`.
- **`PaginatedResult<T>`** — Extends `Result<IEnumerable<T>>` with `Page`, `PageSize`, `TotalCount`, `TotalPages`, `HasNextPage`, `HasPreviousPage`, and `Items`. Factory methods: `Create()`, `Success()`, `Empty()`, `Failure()`.
- **`PaginatedQuery`** — Base record for paginated query requests: `Page` (default 1), `PageSize` (default 20), `SortBy` (nullable string), `SortDirection` (`Ascending`/`Descending`), `MaxPageSize` (virtual, default 100), with normalization helpers `GetNormalizedPage()` and `GetNormalizedPageSize()`.
- **`SortDirection`** — Enum: `Ascending`, `Descending`.
- **`QueryableExtensions`** — Extension methods for `IQueryable<T>` and `IEnumerable<T>`:
  - `ApplySort(sortBy, direction)` / `ApplySort(pagination)` — Dynamic case-insensitive sorting by property path (including nested properties like `"Category.Title"`).
  - `ApplyPagination(pagination)` / `ApplyPagination(page, pageSize, maxPageSize)` — Applies sorting and pagination (`Skip`/`Take`).
  - `ToPaginatedResultAsync(pagination, ct)` / `ToPaginatedResult(pagination)` — Materializes query into a `PaginatedResult<T>` with total count (supporting both EF Core async and in-memory fallback).
- **`ResultExtensions`** — Async extension methods: `MapAsync`, `BindAsync`, `MatchAsync`.

> **Convention:** All operations should return `Result`, `Result<T>`, or `PaginatedResult<T>`. Query endpoints with pagination should inherit from `PaginatedQuery` and use `.ToPaginatedResultAsync(request, ct)`. Never throw exceptions for expected failures — wrap them in `Result.Failure()`.

#### Dynamic Configuration (`Kernel/Configuration/`)
A runtime-mutable, convention-based settings system that reads from JSON files, supports environment overlays and environment variables, integrates directly into ASP.NET's `IConfiguration`, auto-persists changes, and raises events on mutation.

**File naming convention:**
- `appsettings.json` (base root settings) with optional `appsettings.{Environment}.json` (environment overlay).
- `appsettings.{SectionName}.json` (base section settings) with optional `appsettings.{SectionName}.{Environment}.json` (environment overlay).

**Environment Precedence:** Environment Variables (`{SectionName}__{Key}`, `APPSETTINGS__{SectionName}__{Key}`, or `SETTINGS__{SectionName}__{Key}`) > Environment JSON Overlay > Base JSON File. Writes (`SetValue`) always persist to the base file.

- **`DynamicSettingsSection`** — Abstract base class. Subclasses declare `SectionName` (or empty string for root settings) and expose strongly-typed properties via `GetValue<T>(key)` / `SetValue(key, value)`. Has `IsReadonly` guard, `Source` (base file path), `EnvironmentSource` (overlay path), `EnvironmentName`, `MergedValues`, `HasPendingChanges`, `TryGetValue<T>()`, `SetEnvironment(env)`, and `Save()`. `SetValue()` auto-persists to the source file and raises `OnSettingsChanged`.
- **`DynamicSettings`** — Singleton registry of `DynamicSettingsSection` instances, keyed by `SectionName`. `AddSection<T>(instance)` (auto-syncs section environment), `GetSection<T>()`, `GetSection(name)`, `TryGetSection(...)`, `GetSections()`, `SetEnvironment(env)` (switches environment and reloads overlays across all sections), `CommitChanges()`. Exposes `OnSectionAdded`, `OnSettingChanged`, and `OnEnvironmentChanged`. When an `IEventBus` is wired (via `SetEventBus()` in `UseKernel()`), every value change automatically publishes a `SettingsChangedEvent`.
- **`DynamicSettingsConfigurationSource` / `DynamicSettingsConfigurationProvider`** — Bridges `DynamicSettings` into ASP.NET's `IConfiguration` (`builder.Configuration`). Flattens all registered sections into hierarchical keys (`{SectionName}:{Key}` and root keys like `AllowedHosts`, `Logging:...`), and triggers `OnReload()` when settings are modified, when new sections are added, or when the environment changes.
- **`HostSettings`** — Root settings (`SectionName = ""`). Loaded from `appsettings.json` with `appsettings.{Environment}.json` overlay. Exposes `AllowedHosts` and feeds logging configuration to `IConfiguration`.
- **`SettingsChangedEvent`** — `record(SectionName, Key) : Event`. Published via `IEventBus` whenever any section value changes. Subscribe with `IEventHandler<SettingsChangedEvent>` to react to configuration changes.
- **`ApplicationSettings`** — `SectionName = "Application"`. Loaded from `appsettings.Application.json`. Exposes `Environment` and `PartitionAmount`. Its `Environment` property binds to `builder.Environment.EnvironmentName` during startup (`AddKernelDynamicSettings`) and dynamically propagates environment changes across `DynamicSettings` when mutated at runtime.
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

- **`IScopeContext`** — Interface: `UserId` (`DefaultId?`), `IsAuthenticated`, `Roles`, `Claims`, `GetClaim()`, `GetClaimValues()`, `IsInRole()`, `IsSystem`, `CorrelationId`.
- **`WebScopeContext`** — Implementation backed by `IHttpContextAccessor`. Extracts user ID from `NameIdentifier` or `sub` claim, parses to `DefaultId`. Extracts correlation ID from `HttpContext.Items`, `X-Correlation-Id` header, or ambient `Activity`. Caches roles and claims. Has protected constructors accepting `ClaimsPrincipal` and optional `correlationId` for derived contexts. Implements `IsSystem => IsInRole(Constants.Roles.System)`.

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

#### Messaging / Outbox & Inbox (`Kernel/Messaging/`)
A channel-based message persistence, partition-sharded background processing, inbox idempotency, and retention system (outbox + inbox pattern).

- **`Message`** — Entity with `Id`, `Type` (CLR type name), `Content` (serialized JSON), `Channel` (enum), `OccurredAt`, `ProcessedAt`, `ProcessingAt`, `RetryCount`, `NextRetryAt`, `Partition`, `LastError`, `CorrelationId`. Exposes state helper methods: `MarkProcessing()`, `MarkProcessed()`, `MarkFailed()`, `MarkTimedOut()`, `IsProcessingTimedOut()`.
- **`InboxEntry`** — Entity recording processed message IDs (`Id`, `MessageId`, `ProcessedAt`) to ensure idempotent processing and avoid duplicate side effects on retries.
- **`IInbox` / `Inbox`** — Abstraction and EF Core implementation for checking (`HasBeenProcessedAsync`) and recording (`MarkProcessedAsync`) processed message IDs against `KernelDbContext.InboxEntries`.
- **`MessageChannel`** — Enum: `Event`, `Email`.
- **`IMessageWriter` / `MessageWriter`** — Serializes content, assigns a partition via `IPartitionManager`, captures ambient `CorrelationId` from `IScopeContext`, persists to `KernelDbContext.Messages`.
- **`IMessageProcessor`** — Per-channel processor: `GetChannel()`, `ProcessMessage()`.
- **`EventMessageProcessor`** — Processes `MessageChannel.Event` messages: verifies message hasn't been processed via `IInbox`, resolves event type via `IEventTypeRegistry`, deserializes, publishes to `IEventBus` within a `TransactionScope`, and records the message in `IInbox`.
- **`IMessageDispatcher` / `MessageDispatcher`** — Routes messages to the correct `IMessageProcessor` by channel, returning `Task<Result>`.
- **`MessageProcessingWorker`** — Hosted `BaseWorker` running on `ProcessingIntervalSeconds`:
  - **Partitioning**: Only processes messages matching `instanceSettings.Partition` (`[Start..End]`).
  - **Stuck/Timeout Recovery**: Detects messages in `ProcessingAt` exceeding `ProcessingTimeoutSeconds` and marks them timed out for retry.
  - **Exponential Backoff**: Reschedules failed messages with exponential delay ($2^{\text{retry}-1} \times \text{InitialDelay}$, capped at `MaxRetryDelaySeconds`) until `MaxRetryAttempts`.
- **`MessageCleanupWorker`** — Hosted `BaseWorker` running on `CleanupIntervalHours`, deleting processed messages and expired inbox entries older than `CleanupRetentionDays` (and optionally permanently failed messages).
- **`MessagingSettings`** — Dynamic settings loaded from `appsettings.Messaging.json`.

#### Observability & Diagnostics (`Kernel/Diagnostics/`)
Comprehensive HTTP and outbox observability infrastructure with correlation tracking, request duration metrics, and OpenTelemetry diagnostic activities.

- **`DiagnosticsConstants`** — Common diagnostic identifiers: `CorrelationIdHeader` (`"X-Correlation-Id"`), `CorrelationIdItemKey` (`"CorrelationId"`), `ActivitySourceName` (`"Kernel"`), `CorrelationIdTag` (`"correlation.id"`), and static `ActivitySource`.
- **`CorrelationIdMiddleware`** — HTTP middleware that extracts the `X-Correlation-Id` header or generates a new Ulid `DefaultId`. Stores it in `HttpContext.Items`, attaches it to response headers, and tags the ambient `HttpRequest` Activity.
- **`RequestDiagnosticsMiddleware`** — HTTP middleware that measures execution time and emits structured logs: HTTP method, path, status code, duration in milliseconds, correlation ID, and authenticated user ID.
- **`DiagnosticsExtensions`** — Extension methods `app.UseCorrelationId()` and `app.UseRequestDiagnostics()` for configuring the HTTP pipeline in `UseKernel()`.

> **Convention:** All outbound outbox messages automatically capture the ambient request's correlation ID via `IMessageWriter`. Use `IScopeContext.CorrelationId` to correlate logs and operations across services.

#### Structured Audit Log (`Kernel/AuditLog/`)
A centralized, queryable audit trail capturing entity mutations with property-level diffs, user identity, and correlation tracking.

- **`AuditAction`** — Enum: `Create`, `Update`, `Delete`.
- **`AuditPropertyChange`** — Record: `PropertyName`, `OldValue`, `NewValue`.
- **`AuditEntry`** — Entity: `Id` (`DefaultId`), `UserId` (`DefaultId?`), `EntityType`, `EntityId`, `Action` (`AuditAction`), `Changes` (JSON array of `AuditPropertyChange`), `CorrelationId`, `OccurredAt`. Persisted in `KernelDbContext.AuditEntries`.
- **`AuditLogSaveChangesInterceptor`** — Scoped `SaveChangesInterceptor` capturing entity mutations across all `DbContext`s (creates, updates with property diffs, deletes) and persisting `AuditEntry` records into `KernelDbContext`. Skips internal `AuditEntry` and `Message` entities to prevent recursion/noise.
- **`IAuditLogService` / `AuditLogService`** — Service for querying `AuditEntry` records with filtering (`EntityType`, `EntityId`, `UserId`, `Action`, date ranges, `CorrelationId`) and dynamic pagination/sorting via `QueryableExtensions`.
- **`GetAuditEntries` / `GetAuditEntriesHandler`** — CQRS query request extending `PaginatedQuery` and returning `PaginatedResult<AuditEntry>`.
- **`AuditController`** — Administrative endpoint at `api/v1/kernel/audit` protected by `[Authorize(Roles = Roles.System)]`.

> **Convention:** All entity mutations across all DbContexts are automatically captured by `AuditLogSaveChangesInterceptor`. Query audit records via `IAuditLogService` or `GET /api/v1/kernel/audit` (requires `System` role).

#### Caching Abstraction (`Kernel/Caching/`)
A centralized caching abstraction supporting in-memory storage, dynamic configuration, programmatic cache access, and automatic request pipeline response caching:

- **`ICacheService`** — Abstraction for caching operations: `GetAsync<T>`, `SetAsync<T>`, `GetOrCreateAsync<T>`, `RemoveAsync`, `RemoveByPrefixAsync`.
- **`InMemoryCacheService`** — Default in-memory implementation backed by ASP.NET Core's `IMemoryCache`. Features active key tracking with post-eviction callbacks for prefix-based invalidation (`RemoveByPrefixAsync`) and key-level concurrency synchronization to prevent cache stampedes.
- **`CacheSettings`** — `DynamicSettingsSection` subclass (`SectionName => "Cache"`). Loaded from `appsettings.Cache.json` (`Enabled`, `DefaultExpirationSeconds`, `KeyPrefix`).
- **`[Cache]`** / **`CacheAttribute`** — Request interceptor attribute (`Order = -25`) for automatic handler response caching. Supports `DurationSeconds`, `KeyTemplate` (with `{Property}`, `{request.Property}`, `{userId}`, `{correlationId}` placeholders), `KeyPrefix`, and `VaryByUser`.
- **`CacheInterceptor`** — Request pipeline interceptor that resolves key templates dynamically, short-circuits execution on cache hits, and auto-persists successful `Result<TResponse>` values.
- **`AddCaching()`** — Registered automatically in `AddKernel()`.

> **Convention:** Decorate read/query request handlers with `[Cache]` to cache responses. Inject `ICacheService` when programmatic cache manipulation or cache invalidation (`RemoveAsync`, `RemoveByPrefixAsync`) is needed.

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

#### Authorization Policies (`Kernel/Authorization/`)
A multi-layered, policy-based authorization system that secures internally dispatched requests (handlers), Minimal API routes, and MVC controllers with custom business rules.

- **`IAuthorizationPolicy`** — Policy interface: `PolicyName`, `AuthorizeAsync(IScopeContext context, object? resource, CancellationToken ct)`. Evaluates asynchronously and returns `Result`.
- **`AuthenticatedPolicy`** — Built-in policy requiring `context.IsAuthenticated`.
- **`RolePolicy`** — Built-in policy requiring one or more specific roles.
- **`AuthorizationPolicyRegistry`** — Singleton registry of named policies supporting instance registration and DI type activation (`ActivatorUtilities.GetServiceOrCreateInstance`).
- **`AuthorizeAttribute`** — Request handler attribute inheriting from `RequestInterceptorAttribute` (`Order = -100`), auto-resolving the named policy before handler execution.
- **`AuthorizationInterceptor`** — Pipeline interceptor that invokes the named policy and short-circuits with `Result.Failure` on authorization failure.
- **`AuthorizationEndpointFilter`** — `IEndpointFilter` for Minimal APIs. Returns `401 Unauthorized` for unauthenticated requests, `403 Forbidden` for unauthorized requests, or `500` if the policy is missing.
- **`AuthorizePolicyAttribute` & `AuthorizationActionFilter`** — Action filter attribute and filter for MVC controllers.
- **`AuthorizationExtensions`** — Extension methods for `IServiceCollection` (`AddAuthorizationPolicies`, `RegisterAuthorizationPoliciesFromAssembly`) and endpoint route builders (`RequireAuthorizationPolicy`).

> **Convention:** Implement `IAuthorizationPolicy` in any module or Kernel layer. Policies in module assemblies are auto-discovered during `AddModule<T>()`. Protect request handlers with `[Authorize("PolicyName")]`, Minimal APIs with `.RequireAuthorizationPolicy("PolicyName")`, and controllers with `[AuthorizePolicy("PolicyName")]`.

#### Rate Limiting (`Kernel/RateLimiting/`)
A dynamic, partition-based rate limiting subsystem built on ASP.NET Core's `Microsoft.AspNetCore.RateLimiting` and `System.Threading.RateLimiting`, adjustable at runtime via `RateLimitingSettings` and `appsettings.RateLimiting.json`.

- **`RateLimitingSettings`** — `DynamicSettingsSection` subclass (`SectionName => "RateLimiting"`). Exposes:
  - `Enabled` (`bool`, default: `true`).
  - `GlobalPolicy` (`RateLimitPolicyConfig`: `PermitLimit = 200, WindowSeconds = 60, QueueLimit = 0, SegmentsPerWindow = 1`) for unauthenticated requests partitioned by IP.
  - `AuthenticatedPolicy` (`RateLimitPolicyConfig`: `PermitLimit = 500, WindowSeconds = 60, QueueLimit = 0, SegmentsPerWindow = 1`) for authenticated requests partitioned by User ID.
  - `EndpointOverrides` (`Dictionary<string, RateLimitEndpointConfig>`) for specific routes (e.g. `auth/login`, `auth/forgot-password`, `auth/refresh`).
  - `RejectionStatusCode` (`int`, default: `429`).
  - `RejectionMessage` (`string`, default: `"Too many requests. Please try again later."`).
- **`RateLimitingExtensions`** — Extension methods:
  - `builder.AddRateLimiting()` — Registers `RateLimitingSettings` and configures ASP.NET Core `AddRateLimiter` with custom rejection response handling and `PartitionedRateLimiter.Create<HttpContext, string>` evaluating dynamic settings per request.
  - `app.UseRateLimiting()` — Mounts `app.UseRateLimiter()` in the HTTP request pipeline.

> **Convention:** Configure default rate limits in `appsettings.RateLimiting.json`. Rate limiting dynamically adapts to runtime setting changes via `DynamicSettings.SetValue()`.

#### Email Channel (`Kernel/Email/`, `Kernel/Messaging/`)
A complete email delivery abstraction and outbox channel processor supporting attachments and dynamic templates.

- **`EmailMessage`** — Strongly-typed email model: `To`, `Cc`, `Bcc`, `Subject`, `Body`, `IsHtml`, `Attachments` (`List<EmailAttachment>?`).
- **`EmailAttachment`** — Attachment model: `FileName`, `Content` (`byte[]`), `ContentType` (`string?`).
- **`IEmailSender`** — Contract: `SendAsync(EmailMessage message, CancellationToken ct)`.
- **`SmtpEmailSender`** — Default SMTP implementation using `System.Net.Mail.SmtpClient`, configured via `EmailSettings`, with support for multiple recipients and file attachments.
- **`IEmailTemplateRenderer`** — Contract: `RenderAsync(templateName, variables, ct)`.
- **`DefaultEmailTemplateRenderer`** — Simple template renderer supporting token replacement (`{{Key}}`, `{Key}`) and loading templates from files or raw template strings.
- **`EmailSettings`** — `DynamicSettingsSection` subclass (`SectionName => "Email"`). Loaded from `appsettings.Email.json`. Configures `SmtpHost`, `SmtpPort`, `SmtpUsername`, `SmtpPassword`, `EnableSsl`, `FromAddress`, and `FromName`.
- **`EmailMessageProcessor`** — `IMessageProcessor` for `MessageChannel.Email`. Deserializes message payload to `EmailMessage` and sends via `IEmailSender`.

> **Convention:** To send an email through the outbox, call `IMessageWriter.WriteAsync(emailMessage, MessageChannel.Email)`. For direct synchronous delivery, inject `IEmailSender`.

#### Validation Interceptor (`Kernel/Validation/`)
A dispatcher-level request validation subsystem that automatically validates incoming requests before handler execution and short-circuits with structured field errors.

- **`IValidator<T>`** — Validation contract: `ValidationResult Validate(T request)`. Implement this interface to define request validation rules.
- **`ValidationError`** — `record(Field, Message)` representing a single field violation.
- **`ValidationResult`** — Encapsulates `Errors` (`IReadOnlyList<ValidationError>`) and `IsValid` (`bool`). Factory methods: `Success()`, `Failure(params ValidationError[] errors)`, `Failure(string field, string message)`, `Failure(IEnumerable<ValidationError> errors)`.
- **`ValidationException`** — Exception wrapping `ValidationResult` and `Errors`, formatting error summaries. Wrapped inside `Result.Failure<TResponse>()`.
- **`ValidateAttribute`** — Request interceptor attribute (`[Validate]`, `Order = -50`, running after authorization and before logging/transactions). Resolves all registered `IValidator<TRequest>` implementations from DI and executes them.
- **`ValidationInterceptor`** — Pipeline interceptor executing validators and aggregating errors into `Result.Failure` on failure.
- **`ValidationExtensions`** — Extension methods: `AddValidation(assemblies)` and `RegisterValidatorsFromAssembly(assembly)` for registering validators in DI as scoped services.

> **Convention:** Implement `IValidator<YourRequest>` in your feature slice or module. All `IValidator<>` implementations in module assemblies are auto-discovered and registered during `AddModule<T>()`. Decorate your handler with `[Validate]` to enable automated validation.

#### EF Core Persistence Abstractions and Infrastructure (`Kernel/Persistence/`)
- **`IDbContext`** — Common DbContext interface declaring standard EF Core operations (`Database`, `ChangeTracker`, `Set<T>()`, `Entry<T>()`, `SaveChanges()`, `SaveChangesAsync()`).
- **`IDbContextSchema`** — Interface for DbContexts defining a module schema (`string Schema { get; }`).
- **`KernelDbContext`** — Concrete EF Core DbContext implementing `IDbContextSchema` (`Schema => "kernel"`), owning `DbSet<Message> Messages`.
- **`DbContextTypeRegistry`** — Registry of all DbContext types registered across the host and active modules, used for startup table creation.
- **`ModelBuilderExtensions`** — `ApplySchema(schema, database)` extension method that sets the default schema on relational databases and safely bypasses providers without schema support (SQLite, InMemory).
- **`AuditSaveChangesInterceptor`** — Intercepts `SaveChanges` / `SaveChangesAsync` to auto-populate `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy` on `IAuditable` entities using `IScopeContext`.
- **`SoftDeleteSaveChangesInterceptor`** — Intercepts `SaveChanges` / `SaveChangesAsync` to convert `EntityState.Deleted` into `EntityState.Modified` and populate `DeletedAt`, `DeletedBy` on `ISoftDeletable` entities using `IScopeContext`.
- **`DispatchDomainEventsSaveChangesInterceptor`** — Intercepts `SavedChanges` / `SavedChangesAsync` to collect and dispatch `DomainEvents` from `IHasDomainEvents` entities via `IEventBus` and clear them.
- **`DbContextOptionsBuilderExtensions`** — Strongly-typed extension methods (`UseConfiguredDatabase`, `AddKernelInterceptors`) for configuring providers (SqlServer, PostgreSQL, MySql/MariaDb, Sqlite, InMemory) and schema-isolated `MigrationsHistoryTable`.

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
- **`DatabaseHealthCheck`** (`Kernel/Health/`) — Built-in health check testing database connectivity via `KernelDbContext.Database.CanConnectAsync()`.
- **`HealthController`** (`Kernel/Health/Controllers/HealthController.cs`) — ASP.NET Core controller at route `api/v1/kernel/health` (tagged in `kernel` OpenAPI group):
  - `GET api/v1/kernel/health/live` (`[AllowAnonymous]`) — Liveness probe (200 OK).
  - `GET api/v1/kernel/health/ready` (`[AllowAnonymous]`) — Readiness probe (200 OK or 503 Service Unavailable).
  - `GET api/v1/kernel/health/report` (`[Authorize(Roles = Roles.System)]`) — Full system health report breakdown.
  - `GET api/v1/kernel/health/report/{moduleName}` (`[Authorize(Roles = Roles.System)]`) — Detailed module health report.
- **Features** (`Kernel/Health/Features/`) — CQRS requests and handlers: `GetHealthReport` / `GetHealthReportHandler`, `GetModuleHealthReport` / `GetModuleHealthReportHandler`.

> **Convention:** Place module-specific health checks anywhere in your module assembly implementing `IHealthCheck`. The module registration (`AddModule<T>`) automatically discovers and registers all `IHealthCheck` implementations under the module name. Public health probes (`/live`, `/ready`) do not require authorization, whereas detailed reports (`/report`, `/report/{moduleName}`) require `Roles.System`.

#### Data Encryption (`Kernel/Encryption/`)
Column-level authenticated data encryption at rest using AES-256-GCM and transparent Entity Framework Core value converters:

- **`IEncryptionService`** — Interface for encrypting/decrypting strings (Base64 encoded payload) and raw byte arrays.
- **`AesEncryptionService`** — AES-256-GCM AEAD implementation. Automatically generates unique 12-byte random nonces per encryption, calculates 16-byte authentication tags to prevent tampering, and dynamically resolves keys from `EncryptionSettings`.
- **`EncryptionSettings`** — `DynamicSettingsSection` subclass (`SectionName => "Encryption"`). Loaded from `appsettings.Encryption.json`. Supports 32-byte Base64 keys and passphrase key derivation via SHA-256.
- **`[Encrypted]`** / **`EncryptedAttribute`** — Property attribute used on entity string and byte[] properties to mark them for transparent encryption at rest.
- **`EncryptedValueConverter`** / **`EncryptedBinaryValueConverter`** — EF Core `ValueConverter` implementations wrapping `IEncryptionService`.
- **`modelBuilder.UseEncryption(IEncryptionService)`** / **`modelBuilder.UseEncryption(DbContext)`** — Extension methods configuring EF Core entity models for transparent encryption of properties marked with `[Encrypted]`.

> **Convention:** Decorate sensitive entity properties (tokens, credentials, secrets) with `[Encrypted]` and call `modelBuilder.UseEncryption(this)` or `modelBuilder.UseEncryption(encryptionService)` in `OnModelCreating`. Inject `IEncryptionService` when explicit manual data encryption/decryption is required.

#### API Versioning (`Kernel/Versioning/`)
A unified API versioning subsystem based on `Asp.Versioning.Http`, `Asp.Versioning.Mvc`, and `Asp.Versioning.Mvc.ApiExplorer`, configurable at runtime via `VersioningSettings` and `appsettings.Versioning.json`:

- **`VersioningSettings`** — `DynamicSettingsSection` subclass (`SectionName => "Versioning"`). Loaded from `appsettings.Versioning.json`. Exposes:
  - `DefaultVersion` (`string`, default: `"1.0"`).
  - `AssumeDefaultVersionWhenUnspecified` (`bool`, default: `true`).
  - `ReportApiVersions` (`bool`, default: `true`, emits `api-supported-versions` and `api-deprecated-versions` response headers).
  - `ApiVersionHeader` (`string`, default: `"X-Api-Version"`).
  - `ApiVersionQueryParam` (`string`, default: `"api-version"`).
  - `EnableUrlSegmentVersioning` (`bool`, default: `true`).
  - `EnableHeaderVersioning` (`bool`, default: `true`).
  - `EnableQueryVersioning` (`bool`, default: `true`).
  - `GetDefaultApiVersion()` — Parses `DefaultVersion` string into `ApiVersion`.
  - `CreateApiVersionReader()` — Combines active URL-segment, header, and query string version readers.
- **`VersioningExtensions`** — Extension methods:
  - `builder.AddKernelVersioning()` / `services.AddKernelVersioning(settings)` — Configures API versioning, MVC controller versioning, and ApiExplorer version substitution (`GroupNameFormat = "'v'VVV"`, `SubstituteApiVersionInUrl = true`).
- **`IModule.SupportedVersions`** — Default interface property (`static virtual IReadOnlyList<ApiVersion> SupportedVersions => [new ApiVersion(1, 0)];`) allowing modules to declare supported API versions.
- **`ApiVersioningAssertions`** (`Kernel.Testing/Assertions/`) — Fluent assertions (`ShouldHaveSupportedApiVersions()`, `ShouldHaveDeprecatedApiVersions()`) on `HttpResponseMessage`.

> **Convention:** Version controllers using `[ApiVersion(1.0)]` and `[Route("api/v{version:apiVersion}/...")]`. Minimal APIs can map versioned route groups using `app.NewApiVersionSet()`. Configure global default versioning behavior in `appsettings.Versioning.json`.

#### File Storage (`Kernel/Storage/`)
A unified file and blob storage subsystem with a local filesystem provider, runtime-mutable settings, path traversal protection, size limits, and test fakes:

- **`IFileStorage`** — Abstraction for storing, retrieving, and deleting files and binary blobs (`UploadAsync`, `DownloadAsync`, `DeleteAsync`, `GetUrlAsync`, `ExistsAsync`).
- **`LocalFileStorage`** — Default filesystem provider. Features directory auto-creation, path traversal attack prevention (`../`, `..\`), file size limit enforcement, and error isolation with railway-oriented `Result`.
- **`FileStorageSettings`** — `DynamicSettingsSection` subclass (`SectionName => "Storage"`). Loaded from `appsettings.Storage.json`. Exposes:
  - `Provider` (`string`, default: `"Local"`).
  - `BasePath` (`string`, default: `"./storage"`).
  - `EnablePublicAccess` (`bool`, default: `true`).
  - `MaxFileSizeBytes` (`long`, default: `10485760` / 10MB; 0 = unlimited).
  - `AllowedExtensions` (`IReadOnlyList<string>`, default: `[]`).
  - `AutoCreateDirectory` (`bool`, default: `true`).
- **`FileRegistryEntry`** — Entity mapping an opaque random identifier (`DefaultId` / Ulid) to an internal storage path with optional expiry.
- **`IFileRegistry` / `DbFileRegistry`** — Abstraction and EF Core implementation for registering, resolving, and revoking opaque public file IDs.
- **`FilesController`** (`Kernel/Storage/Controllers/`) — Public API controller (`GET /files/{id}`) serving files by their registered opaque identifier. Features automatic MIME detection, HTTP range processing, and optional download attachment header.
- **`StorageExtensions`** (`Kernel/Storage/Extensions/`) — Extension methods:
  - `storage.UploadAsync(path, byte[], ...)` / `storage.DownloadBytesAsync(path, ...)`
  - `storage.UploadTextAsync(path, text, ...)` / `storage.DownloadTextAsync(path, ...)`
  - `services.AddFileStorage()` — Registers `IFileRegistry` -> `DbFileRegistry` and `IFileStorage` -> `LocalFileStorage`.
- **`FakeFileStorage`** (`Kernel.Testing/Fakes/`) — Thread-safe in-memory test fake tracking stored files, deleted paths, call counters (`UploadCount`, `DownloadCount`, `DeleteCount`), failure simulation flags, and `Registry` (`FakeFileRegistry`).
- **`FakeFileRegistry`** (`Kernel.Testing/Fakes/`) — In-memory test fake implementing `IFileRegistry`.
- **`FileStorageAssertions`** (`Kernel.Testing/Assertions/`) — Fluent assertions: `storage.ShouldHaveFile(path)`, `storage.ShouldHaveFile(path, bytes)`, `storage.ShouldHaveFile(path, text)`, `storage.ShouldNotHaveFile(path)`, `storage.ShouldHaveDeleted(path)`, `storage.ShouldBeEmpty()`, `storage.ShouldHaveCount(count)`.

#### Real-Time Notifications (`Kernel/Notifications/`)
A real-time notification abstraction backed by ASP.NET Core SignalR, configurable via dynamic settings, targeting individual users, named groups, or all connected clients:

- **`INotificationService`** — Abstraction for pushing real-time notifications:
  - `SendToUserAsync(userId, channel, payload, ct)` — Pushes notification to a specific user across all active connections.
  - `SendToGroupAsync(group, channel, payload, ct)` — Pushes notification to all connected clients in a named group.
  - `SendToAllAsync(channel, payload, ct)` — Broadcasts notification to all connected clients.
- **`SignalRNotificationService`** — Default SignalR implementation using `IHubContext<NotificationHub>`. Respects `NotificationSettings.Enabled` and invokes both generic `ReceiveNotification(channel, payload)` and named channel methods on clients.
- **`NotificationHub`** — SignalR `Hub` implementation providing `JoinGroup(group)` and `LeaveGroup(group)` client methods along with connection lifecycle logging.
- **`KernelUserIdProvider`** — `IUserIdProvider` implementation resolving user identifier from standard claims (`sub`, `ClaimTypes.NameIdentifier`, `ClaimTypes.Name`) aligned with `IScopeContext`.
- **`NotificationSettings`** — `DynamicSettingsSection` subclass (`SectionName => "Notifications"`). Loaded from `appsettings.Notifications.json`. Exposes:
  - `Enabled` (`bool`, default: `true`).
  - `Path` (`string`, default: `"/hubs/notifications"`).
- **`NotificationExtensions`** (`Kernel/Notifications/Extensions/`) — Extension methods:
  - `services.AddNotifications()` — Registers SignalR, `KernelUserIdProvider`, and `SignalRNotificationService`.
  - `app.MapNotifications()` — Maps `NotificationHub` at `NotificationSettings.Path`.
- **`FakeNotificationService`** (`Kernel.Testing/Fakes/`) — In-memory test fake capturing sent notifications (`SentNotifications`, `GetSentToUser`, `GetSentToGroup`, `GetBroadcastNotifications`, `HasSentToUser`, `HasSentToGroup`, `HasSentBroadcast`, `Clear`).
- **`NotificationAssertions`** (`Kernel.Testing/Assertions/`) — Fluent assertions: `service.ShouldHaveSentToUser(userId)`, `service.ShouldHaveSentToGroup(group)`, `service.ShouldHaveSentBroadcast()`, `service.ShouldNotHaveSentNotifications()`, `service.ShouldHaveNotificationCount(count)`.

> **Convention:** Inject `INotificationService` into event handlers, workers, or controllers to push real-time updates to users or groups. In tests, use `fixture.NotificationService` and assert with `fixture.NotificationService.ShouldHaveSentToUser(...)`.
 
### Single Page Application (SPA) Serving (`Kernel/Spa/`)
Full-lifecycle SPA serving supporting **both single and multiple SPAs** (Host-level and Module-defined), dev server reverse proxying, optional background dev server auto-execution, and production static file delivery:

- **`SpaAppOptions`** — Configuration model for an individual SPA: `Name`, `Owner` (e.g. `"Host"`, `"Identity"`), `BasePath` (normalized prefix e.g. `"/"`, `"/auth"`, `"/auth/admin"`), `DevServerUrl`, `SourcePath`, `DistPath`, `FallbackFile`, `Enabled`, `StripBasePathOnProxy`, `DevServerCommand` (e.g. `"npm run dev"`), and `AutoStartDevServer` (`bool`, default: `false`).
- **`SpaRegistry`** — Central singleton registry aggregating SPAs across the Host and all active modules. `GetOrderedApps()` automatically sorts SPAs by `BasePath.Length` descending so nested/specific prefixes (e.g. `/auth/admin` length 11) are evaluated before parent paths (`/auth` length 5) and root (`/` length 1).
- **`SpaProcessManager`** — Manages the lifecycle of frontend development servers when `AutoStartDevServer` is enabled. Cross-platform process launcher (cmd/sh) with TCP port probing to avoid duplicate instances, stdout/stderr forwarding to `ILogger`, and process tree termination via `IHostApplicationLifetime`.
- **`SpaSettings`** — `DynamicSettingsSection` subclass (`SectionName => "Spa"`). Loaded from `appsettings.Spa.json`. Contains `Enabled`, `ProxyTimeoutSeconds`, `ExcludedPrefixes` (defaults: `["/api", "/openapi", "/scalar", "/docs", "/hubs", "/health", "/files"]`), and `Apps` dictionary.
- **`SpaHtmlTemplates`** — HTML diagnostic templates:
  - Development: Modern dark-themed offline diagnostic page displaying the exact SPA name, owner, base path, target URL, and `npm run dev` command when a dev server is unreachable.
  - Production: Informative warning page when `DistPath` compiled assets are missing.
- **`SpaExtensions`** (`Kernel/Spa/Extensions/`):
  - `services.AddSpaServices()` — Registers `SpaRegistry`, `SpaProcessManager`, YARP `IHttpForwarder`, and high-performance pooled `HttpMessageInvoker`.
  - `services.AddModuleSpa(moduleName, options)` / `services.AddModuleSpa(moduleName, appName, options)` / `services.AddModuleSpas(...)` — Fluent helper for modules to register one or more owned SPAs during `IModule.Register`.
  - `app.UseSpa()` — Configures the HTTP pipeline:
    - **Development**: Optionally starts dev server commands if `AutoStartDevServer = true`, and reverse-proxies unhandled requests under each SPA's `BasePath` to its configured `DevServerUrl`, forwarding HTTP requests and **WebSockets** (for Vite/Webpack HMR).
    - **Production**: Mounts `UseDefaultFiles` and `UseStaticFiles` for each SPA's `DistPath` at its `BasePath`, and maps fallback to `FallbackFile` (`index.html`) for client-side navigation.
    - **API Route Protection**: Requests matching `ExcludedPrefixes` (`/api`, `/openapi`, `/scalar`, `/docs`, `/hubs`, `/health`, `/files`) are never intercepted by any SPA and return clean 404 responses.
- **`SpaAssertions`** (`Kernel.Testing/Assertions/`) — Fluent assertions: `registry.ShouldHaveApp(name)`, `registry.ShouldHaveAppWithBasePath(name, basePath)`, `registry.ShouldHaveAppCount(count)`, `registry.ShouldHaveEvaluationOrder(...)`, `app.ShouldHaveDevServerCommand(command)`, `app.ShouldAutoStartDevServer()`, `settings.ShouldBeEnabled()`, `settings.ShouldExcludePrefix(prefix)`.

> **Convention:** In development, each SPA can either run independently in a terminal or be auto-started by setting `"AutoStartDevServer": true` and `"DevServerCommand": "npm run dev"`. In production, run `npm run build` in each SPA's directory. Register module SPAs in `IModule.Register` via `services.AddModuleSpa(Name, ...)`. If a module is deactivated in `ActiveModules`, all of its SPAs are automatically excluded from the pipeline.

#### Testing Utilities (`Kernel.Testing/`)
A companion class library providing test fakes, base test fixtures, and fluent assertion helpers for testing handlers, modules, and web hosts in an isolated in-memory environment.

- **`FakeEventBus`** (`Kernel.Testing/Fakes/`) — Thread-safe test fake implementing `IEventBus`. Captures published events in chronological order, provides querying (`GetPublishedEvents<T>`, `GetLastPublishedEvent<T>`, `HasPublished<T>`, `CountPublished<T>`), and supports assertion helpers.
- **`FakeScopeContext`** (`Kernel.Testing/Fakes/`) — Test fake implementing `IScopeContext` with a fluent setup API (`AsAnonymous()`, `AsAuthenticated(userId, roles)`, `AsSystem()`, `WithRole()`, `WithClaim()`, `WithCorrelationId()`, `Reset()`).
- **`FakeEmailSender`** (`Kernel.Testing/Fakes/`) — Test fake implementing `IEmailSender`. Records sent messages (`SentMessages`, `LastSentMessage`, `SentCount`, `HasSent`, `GetSentTo`) and allows simulating errors via `ShouldSucceed` and `FailureError`.
- **`FakeCacheService`** (`Kernel.Testing/Fakes/`) — In-memory test fake implementing `ICacheService`. Supports inspecting keys, values, and expiration without external Redis or memory cache dependencies.
- **`FakeEncryptionService`** (`Kernel.Testing/Fakes/`) — Test fake implementing `IEncryptionService`. Supports transparent prefix transformation or real AES mode, failure simulation, and tracks all encryption/decryption operations.
- **`FakeFileStorage`** (`Kernel.Testing/Fakes/`) — In-memory test fake implementing `IFileStorage`. Tracks stored blobs, deleted paths, and call counts.
- **`FakeNotificationService`** (`Kernel.Testing/Fakes/`) — In-memory test fake implementing `INotificationService`. Tracks sent notifications by target (user, group, broadcast) and channel.
- **`ResultAssertions`** (`Kernel.Testing/Assertions/`) — Fluent assertion extensions:
  - `result.ShouldBeSuccess()` — Asserts success, returns `Value` for `Result<T>`.
  - `result.ShouldBeFailure()` / `result.ShouldBeFailure<TException>()` — Asserts failure and error exception type.
  - `result.ShouldHaveValidationError(field)` — Asserts `ValidationException` and error on specific field.
  - `result.ShouldHavePage(page, pageSize, totalCount)` — Asserts pagination metadata on `PaginatedResult<T>`.
- **`EventBusAssertions`** (`Kernel.Testing/Assertions/`) — `ShouldHavePublished<T>()`, `ShouldHavePublished<T>(predicate)`, `ShouldHavePublished<T>(count)`, `ShouldNotHavePublished<T>()`, `ShouldBeEmpty()`.
- **`EmailSenderAssertions`** (`Kernel.Testing/Assertions/`) — `ShouldHaveSent()`, `ShouldHaveSent(predicate)`, `ShouldHaveSentTo(email)`, `ShouldHaveSentCount(count)`, `ShouldNotHaveSent()`.
- **`CacheAssertions`** (`Kernel.Testing/Assertions/`) — `ShouldContainKey(key)`, `ShouldContain<T>(key, expectedValue)`, `ShouldNotContainKey(key)`, `ShouldBeEmpty()`, `ShouldHaveCount(count)`.
- **`EncryptionAssertions`** (`Kernel.Testing/Assertions/`) — `ShouldHaveEncrypted(plaintext)`, `ShouldHaveDecrypted(ciphertext)`, `ShouldHaveEncryptedCount(count)`, `ShouldHaveDecryptedCount(count)`, `ShouldBeEmpty()`.
- **`ApiVersioningAssertions`** (`Kernel.Testing/Assertions/`) — `ShouldHaveSupportedApiVersions(versions)`, `ShouldHaveDeprecatedApiVersions(versions)`.
- **`FileStorageAssertions`** (`Kernel.Testing/Assertions/`) — `ShouldHaveFile(path)`, `ShouldHaveFile(path, bytes)`, `ShouldHaveFile(path, text)`, `ShouldNotHaveFile(path)`, `ShouldHaveDeleted(path)`, `ShouldBeEmpty()`, `ShouldHaveCount(count)`.
- **`NotificationAssertions`** (`Kernel.Testing/Assertions/`) — `ShouldHaveSentToUser(userId)`, `ShouldHaveSentToGroup(group)`, `ShouldHaveSentBroadcast()`, `ShouldNotHaveSentNotifications()`, `ShouldHaveNotificationCount(count)`.
- **`SpaAssertions`** (`Kernel.Testing/Assertions/`) — `ShouldHaveApp(name)`, `ShouldHaveAppWithBasePath(name, basePath)`, `ShouldHaveAppCount(count)`, `ShouldHaveEvaluationOrder(names)`, `ShouldHaveBasePath(path)`, `ShouldHaveDevServerUrl(url)`, `ShouldHaveOwner(owner)`, `ShouldHaveDistPath(path)`, `ShouldBeEnabled()`, `ShouldExcludePrefix(prefix)`.
- **`KernelTestBase`** (`Kernel.Testing/Infrastructure/`) — Base test fixture providing an isolated, in-memory DI container, in-memory `KernelDbContext`, pre-configured fakes (`EventBus`, `ScopeContext`, `EmailSender`, `CacheService`, `EncryptionService`, `FileStorage`, `NotificationService`), `Dispatcher`, and scope execution helpers (`ExecuteScopeAsync`, `ExecuteDbContextAsync`).
- **`ModuleTestBase<TModule>` / `ModuleTestBase<TModule, TDbContext>`** (`Kernel.Testing/Infrastructure/`) — Extends `KernelTestBase` to automatically register a module's services, request handlers, event handlers, validators, authorization policies, and health checks, and pre-seeds declared roles and ensures module tables are created.
- **`TestWebApplicationFactory<TProgram>`** (`Kernel.Testing/Infrastructure/`) — Pre-configured `WebApplicationFactory` for full integration testing of the Host and active modules with an in-memory database and test fakes for `EventBus`, `EmailSender`, `CacheService`, `EncryptionService`, `FileStorage`, and `NotificationService`, plus a built-in test authentication handler (`CreateAuthenticatedClient(userId, roles)`).

> **Convention:** In test projects, reference `Kernel.Testing.csproj`. For unit/handler tests of a module, inherit from `ModuleTestBase<MyModule, MyDbContext>`. Use `Dispatcher.DispatchAsync(request)`, configure identity with `ScopeContext.AsSystem()` or `ScopeContext.AsAuthenticated()`, assert outcomes with `.ShouldBeSuccess()`, and assert event publications with `EventBus.ShouldHavePublished<MyEvent>()`. For full HTTP endpoint integration tests, use `TestWebApplicationFactory<Program>`.

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
- **Endpoints & Routing**: All API endpoints must start with `api/<version>/` (e.g., `api/v1/kernel/settings`, `api/v1/orders`). Group endpoints using `.WithTags("{ModuleName}").WithGroupName("{ModuleName}")`.
- **Extension methods** for `WebApplicationBuilder` go in files named `WebApplicationBuilderExtensions.cs` within their feature folder.

### Controller & Endpoint Structure
- **API Versioning**: Every API controller must be decorated with `[ApiController]`, `[ApiVersion(1.0)]`, and `[Route("api/v{version:apiVersion}/...")]`.
- **Endpoint Regions**: On controllers, each action method must be enclosed in its own `#region ActionName` ... `#endregion` block.
- **Request Body Records**: If an endpoint accepts a request body model/record, declare the record directly on top of the corresponding action method inside its `#region` block, rather than at the bottom or outside the controller class.

### Authorization & Default Policies
- **Default Roles**: By default, standard users have **no roles** assigned. The only built-in role is `Roles.System` (`"System"`), reserved for super-user operations.
- **Default Policies**: By default, only two authorization policies exist:
  - `Authenticated` (`AuthenticatedPolicy` / `IdentityPolicies.Authenticated`): Requires an authenticated user (`IScopeContext.IsAuthenticated`).
  - `System` (`SystemPolicy` / `IdentityPolicies.System`): Requires the system super-user role (`IScopeContext.IsSystem`).
- **Policy Usage Preference**: Always prefer using strongly-typed Kernel authorization policies (`[AuthorizePolicy<AuthenticatedPolicy>]` and `[AuthorizePolicy<SystemPolicy>]` on controllers, or `.RequireAuthorizationPolicy<AuthenticatedPolicy>()` and `.RequireAuthorizationPolicy<SystemPolicy>()` on Minimal API endpoints) over raw role strings or framework-level `[Authorize]` attributes.

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
| New message channel + processor | `Kernel/Messaging/` |
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
