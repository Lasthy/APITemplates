# Shopping — Agent Guide

This repository is a **Modular Web API** (MWA) monolith in ASP.NET (.NET 10). It organizes features into decoupled, pluggable modules sharing a common Kernel and Data layer, utilizing companion contract projects for cross-module communication.

---

## Architecture Overview

```
Solution Root (Shopping.slnx)
├── Host/                    # ASP.NET Web App — entry point, pipeline, static appsettings
│   └── Program.cs           # builder.AddKernel() + builder.AddData() + builder.AddModules()
├── Kernel/                  # Shared class library — all cross-cutting concerns & persistence
│   ├── AuditLog/            # Structured audit logging (AuditEntry, SaveChanges interceptor, service, controller)
│   ├── Authorization/       # Policy-based authorization (IAuthorizationPolicy, registry, interceptor, filters)
│   ├── Caching/             # Caching abstraction (ICacheService, InMemoryCacheService, CacheAttribute, CacheInterceptor, CacheSettings)
│   ├── Common/              # Result pattern (Result, Result<T>, PaginatedResult<T>)
│   ├── Configuration/       # Dynamic settings system (DynamicSettingsSection, DynamicSettings, DataSettings)
│   ├── Constants/           # Application-wide constants (Roles)
│   ├── Context/             # Scoped operation context (IScopeContext, WebScopeContext)
│   ├── Diagnostics/         # Observability & diagnostics (CorrelationId, RequestDiagnostics, ActivitySource)
│   ├── Email/               # Email delivery abstraction (IEmailSender, SmtpEmailSender, EmailMessage, EmailSettings, template renderer)
│   ├── Encryption/          # Column-level data encryption at rest (IEncryptionService, AesEncryptionService, [Encrypted], EncryptedValueConverter, EncryptionSettings)
│   ├── Entities/            # Entity marker interfaces (IAuditable, ISoftDeletable, IHasDomainEvents)
│   ├── Events/              # EventBus abstraction + in-process impl + registry
│   ├── Extensions/          # WebApplicationBuilder extensions (AddKernel, AddData, UseKernel, UseData)
│   ├── Health/              # Health check models + checks (MemoryHealthCheck, DatabaseHealthCheck)
│   ├── Json/                # Centralized JSON serializer abstraction (IJsonSerializer)
│   ├── Messaging/           # Outbox/Inbox message persistence + processing (Kernel.Boxing)
│   ├── Modules/             # IModule contract + module registration infrastructure
│   ├── Notifications/       # Real-time notifications abstraction (INotificationService, SignalRNotificationService, NotificationHub, NotificationSettings, extensions)
│   ├── Partitioning/        # Partition management for multi-instance deployments
│   ├── Persistence/         # IDbContext, IDbContextSchema, KernelDbContext, DbContextTypeRegistry + EF SaveChanges interceptors
│   ├── RateLimiting/        # Dynamic, partition-based rate limiting (RateLimitingSettings, extensions)
│   ├── Requests/            # Request/Response + Dispatcher + Interceptor interfaces
│   ├── Storage/             # File storage abstraction (IFileStorage, LocalFileStorage, FileStorageSettings, extensions)
│   ├── Ulid/                # Vendored Ulid implementation + EF/JSON/Type converters
│   ├── Versioning/          # URL/header/query API versioning (VersioningSettings, VersioningExtensions)
│   ├── Workers/             # BaseWorker background service (periodic / one-shot)
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
- **Modules are self-contained projects.** Each module has its own `appsettings.{ModuleName}.json`, its own EF Core `DbContext` (with schema isolation), and implements `IModule` for DI and endpoint registration.
- **Companion Contract Projects for Cross-Module Communication.** Modules expose public contracts (events, requests, DTOs) via a companion `{ModuleName}.Contracts` project. A module may reference another module's `.Contracts` project, but **never** its implementation project.
- **Module activation is config-driven.** The `InstanceSettings` section in `appsettings.Instance.json` has an `ActiveModules` dictionary (`{ "ModuleName": true/false }`) controlling which modules are loaded at startup.
- **`DefaultId` is `System.Ulid` everywhere.** Defined in `GlobalUsings.Shared.cs` and propagated to all projects via `Directory.Build.props`.

---

## Project Breakdown

### Host (`Host/`)

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

**Program.cs composition pattern:**
```csharp
builder.AddKernel();      // Register kernel services + kernel OpenAPI doc + dynamic settings into IConfiguration
builder.AddData();        // Register database options and DbContexts
builder.AddModules();     // Discover + register active modules
var app = builder.Build();
app.UseKernel();          // Map /openapi/kernel.json
app.UseData();            // Ensure databases and tables are created automatically on startup
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

### Kernel (`Kernel/`)

A class library (not a web project) that provides all shared infrastructure, database providers (SqlServer, PostgreSQL, MySql/MariaDb, Sqlite, InMemory), persistence abstractions, and cross-cutting concerns. It targets `net10.0` and references `Microsoft.AspNetCore.App`, Entity Framework Core, and OpenAPI packages.

---

## Cross-Module Communication

Cross-module communication follows the **Companion Contract Projects (`*.Contracts`)** pattern:

```
Modules/
├── Orders/                                  # Module implementation
│   ├── Features/
│   │   └── GetOrderSummaryHandler.cs        # Implements IRequestHandler from Orders.Contracts
│   ├── Orders.csproj                        # References Kernel & Orders.Contracts
│   └── OrdersModule.cs
│
├── Orders.Contracts/                        # Public contracts (shareable)
│   ├── Dtos/OrderSummaryDto.cs
│   ├── Events/OrderPlacedEvent.cs           # Inherits Event
│   ├── Requests/GetOrderSummary.cs          # Implements IRequest<OrderSummaryDto>
│   └── Orders.Contracts.csproj              # References Kernel
│
└── Notifications/                           # Another module
    ├── Features/
    │   └── SendOrderEmailHandler.cs         # Implements IEventHandler<OrderPlacedEvent>
    └── Notifications.csproj                 # References Kernel & Orders.Contracts
```

### 1. Synchronous Queries/Commands (via `IDispatcher`)
When `Notifications` needs data from `Orders`:
1. Reference `Orders.Contracts` in `Notifications.csproj`.
2. Inject `IDispatcher` and send the request:
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

---

## Core Features & Conventions

### Standardized Result & Pagination (`Kernel/Common/`)
Railway-oriented result pattern for operation outcomes and standardized query pagination and sorting.

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

### Dynamic Configuration (`Kernel/Configuration/`)
A runtime-mutable, convention-based settings system that reads from JSON files, supports environment overlays and environment variables, integrates directly into ASP.NET's `IConfiguration`, auto-persists changes, and raises events on mutation.

**File naming convention:**
- `appsettings.json` (base root settings) with optional `appsettings.{Environment}.json` (environment overlay).
- `appsettings.{SectionName}.json` (base section settings) with optional `appsettings.{SectionName}.{Environment}.json` (environment overlay).

**Environment Precedence:** Environment Variables (`{SectionName}__{Key}`, `APPSETTINGS__{SectionName}__{Key}`, or `SETTINGS__{SectionName}__{Key}`) > Environment JSON Overlay > Base JSON File. Writes (`SetValue`) always persist to the base file.

- **`DynamicSettingsSection`** — Abstract base class. Subclasses declare `SectionName` (or empty string for root settings) and expose strongly-typed properties via `GetValue<T>(key)` / `SetValue(key, value)`. `SetValue()` auto-persists to the source file and raises `OnSettingsChanged`.
- **`DynamicSettings`** — Singleton registry of `DynamicSettingsSection` instances.
- **`DynamicSettingsConfigurationSource` / `DynamicSettingsConfigurationProvider`** — Bridges `DynamicSettings` into ASP.NET's `IConfiguration` (`builder.Configuration`). Flattens all registered sections into hierarchical keys and triggers `OnReload()` when settings are modified or new sections are added.
- **`HostSettings`** — Root settings (`SectionName = ""`). Loaded from `appsettings.json` with `appsettings.{Environment}.json` overlay. Exposes `AllowedHosts` and feeds logging configuration to `IConfiguration`.
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

### Messaging, Outbox & Inbox (`Kernel/Messaging/`)
Channel-based outbox message persistence, partition-sharded background processing, inbox idempotency, fault tolerance, and retention management:

- **`Message`** — Entity storing serialized payload, CLR type, channel, partition, and lifecycle timestamps (`OccurredAt`, `ProcessingAt`, `ProcessedAt`, `NextRetryAt`, `RetryCount`, `LastError`).
- **`InboxEntry`** — Entity storing processed message IDs (`Id`, `MessageId`, `ProcessedAt`) to guarantee idempotency and avoid duplicate processing upon retries.
- **`IInbox` / `Inbox`** — Abstraction and EF Core implementation for checking (`HasBeenProcessedAsync`) and marking (`MarkProcessedAsync`) processed messages.
- **`IMessageWriter` / `MessageWriter`** — Serializes content, assigns a random partition via `IPartitionManager`, and persists to `KernelDbContext.Messages`.
- **`IMessageProcessor`** — Per-channel processing contract (`GetChannel()`, `ProcessMessage()`). Implementations: `EventMessageProcessor` (with `IInbox` idempotency guard for `MessageChannel.Event`), `EmailMessageProcessor` for `MessageChannel.Email`.
- **`IMessageDispatcher` / `MessageDispatcher`** — Routes messages by channel to their corresponding processor, returning `Task<Result>`.
- **`MessageProcessingWorker`** — Hosted `BaseWorker` running on `ProcessingIntervalSeconds`:
  - **Partitioning**: Only processes messages matching `instanceSettings.Partition` (`[Start..End]`).
  - **Stuck/Timeout Recovery**: Detects messages in `ProcessingAt` exceeding `ProcessingTimeoutSeconds` and marks them timed out for retry.
  - **Exponential Backoff**: Reschedules failed messages with exponential delay ($2^{\text{retry}-1} \times \text{InitialDelay}$, capped at `MaxRetryDelaySeconds`) until `MaxRetryAttempts`.
- **`MessageCleanupWorker`** — Hosted `BaseWorker` running on `CleanupIntervalHours`, deleting processed messages and expired inbox entries older than `CleanupRetentionDays` (and optionally permanently failed messages).
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

- **`IDbContext`** — Common interface for database contexts exposing standard EF Core operations.
- **`IDbContextSchema`** — Interface for DbContexts defining a module schema (`string Schema { get; }`).
- **`KernelDbContext`** — Concrete DbContext for Kernel entities (`DbSet<Message> Messages`, `DbSet<InboxEntry> InboxEntries`, `DbSet<AuditEntry> AuditEntries`).
- **`DbContextTypeRegistry`** — Registry of all DbContext types registered across the host and active modules.
- **`ModelBuilderExtensions`** — `ApplySchema(schema, database)` extension method that sets the default schema on relational databases and safely bypasses providers without schema support (SQLite, InMemory).
- **`AuditSaveChangesInterceptor`** — Automatically sets `CreatedAt`/`CreatedBy` on entity addition and `UpdatedAt`/`UpdatedBy` on entity modification using `IScopeContext`.
- **`SoftDeleteSaveChangesInterceptor`** — Converts hard deletes on `ISoftDeletable` entities into soft deletes (`EntityState.Modified`), updating `DeletedAt` and `DeletedBy`.
- **`DispatchDomainEventsSaveChangesInterceptor`** — Automatically collects and dispatches `DomainEvents` from `IHasDomainEvents` entities through `IEventBus` upon saving and clears them.
- **`DbContextOptionsBuilderExtensions`** — Strongly-typed extension methods (`UseConfiguredDatabase`, `AddKernelInterceptors`) for configuring providers (SqlServer, PostgreSQL, MySql/MariaDb, Sqlite, InMemory) and schema-isolated `MigrationsHistoryTable`.

### Module System (`Kernel/Modules/`)
Pluggable module architecture:

- **`IModule`** — Contract:
  - `static abstract string Name` — module identifier; must match the key in `ActiveModules`.
  - `static abstract void Register(IServiceCollection, IConfiguration)` — DI registration.
  - `static abstract void MapEndpoints(IEndpointRouteBuilder)` — route registration.
- **`AddModules()`** / **`UseModules()`** — Discovers active modules from `appsettings.Instance.json`, registers dynamic settings, scans and registers event handlers (`IEventHandler<>`), request handlers (`IRequestHandler<,>`), and health checks (`IHealthCheck`), registers OpenAPI docs, and maps endpoints.

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

### Rate Limiting (`Kernel/RateLimiting/`)
Dynamic, partition-based rate limiting built on `Microsoft.AspNetCore.RateLimiting`:

- **`RateLimitingSettings`** — `DynamicSettingsSection` subclass (`SectionName => "RateLimiting"`). Loaded from `appsettings.RateLimiting.json`. Configures `GlobalPolicy` (per IP), `AuthenticatedPolicy` (per User ID), and `EndpointOverrides` (sensitive routes like `auth/login`).
- **`AddRateLimiting()`** / **`UseRateLimiting()`** — Automatically registered and mounted in `AddKernel()` and `UseKernel()`. Dynamically adapts to runtime configuration changes.

### Email Channel (`Kernel/Email/`, `Kernel/Messaging/`)
A complete email delivery abstraction and outbox channel processor supporting attachments and dynamic templates:

- **`EmailMessage`** & **`EmailAttachment`** — Strongly-typed email model with recipient lists, subject, HTML body, and binary attachments.
- **`IEmailSender`** & **`SmtpEmailSender`** — Email sender abstraction and default SMTP implementation using `System.Net.Mail.SmtpClient`.
- **`IEmailTemplateRenderer`** & **`DefaultEmailTemplateRenderer`** — Simple template renderer supporting token replacement (`{{Key}}`, `{Key}`) and loading templates from files or raw template strings.
- **`EmailSettings`** — `DynamicSettingsSection` subclass (`SectionName => "Email"`). Loaded from `appsettings.Email.json`.
- **`EmailMessageProcessor`** — `IMessageProcessor` for `MessageChannel.Email`. Deserializes message payload to `EmailMessage` and sends via `IEmailSender`.

### Validation Interceptor (`Kernel/Validation/`)
A dispatcher-level request validation subsystem that automatically validates incoming requests before handler execution and short-circuits with structured field errors:

- **`IValidator<T>`** — Validation contract: `ValidationResult Validate(T request)`.
- **`ValidationError`** & **`ValidationResult`** — Structured validation error and result objects.
- **`ValidationException`** — Exception wrapping `ValidationResult` and `Errors`, formatted into `Result.Failure`.
- **`[Validate]`** / **`ValidateAttribute`** — Request interceptor attribute (`Order = -50`), executing all registered `IValidator<TRequest>` implementations.
- **`ValidationInterceptor`** — Request pipeline interceptor executing validators and aggregating errors.
- **`AddValidation()`** / **`RegisterValidatorsFromAssembly()`** — Automatically scans and registers validators in DI during `AddKernel()` and `AddModule<T>()`.

### Observability & Diagnostics (`Kernel/Diagnostics/`)
Comprehensive HTTP and outbox observability infrastructure with correlation tracking, request duration metrics, and OpenTelemetry diagnostic activities:

- **`DiagnosticsConstants`** — Common diagnostic identifiers: `CorrelationIdHeader` (`"X-Correlation-Id"`), `CorrelationIdItemKey` (`"CorrelationId"`), `ActivitySourceName` (`"Kernel"`), `CorrelationIdTag` (`"correlation.id"`), and static `ActivitySource`.
- **`CorrelationIdMiddleware`** — HTTP middleware that extracts the `X-Correlation-Id` header or generates a new Ulid `DefaultId`. Stores it in `HttpContext.Items`, attaches it to response headers, and tags the ambient `HttpRequest` Activity.
- **`RequestDiagnosticsMiddleware`** — HTTP middleware that measures execution time and emits structured logs: HTTP method, path, status code, duration in milliseconds, correlation ID, and authenticated user ID.
- **`DiagnosticsExtensions`** — Extension methods `app.UseCorrelationId()` and `app.UseRequestDiagnostics()` for configuring the HTTP pipeline in `UseKernel()`.

> **Convention:** All outbound outbox messages automatically capture the ambient request's correlation ID via `IMessageWriter`. Use `IScopeContext.CorrelationId` to correlate logs and operations across services.

#### Structured Audit Log (`Kernel/AuditLog/`)
A centralized, queryable audit trail capturing entity mutations with property-level diffs, user identity, and correlation tracking:

- **`AuditAction`** — Enum: `Create`, `Update`, `Delete`.
- **`AuditPropertyChange`** — Record: `PropertyName`, `OldValue`, `NewValue`.
- **`AuditEntry`** — Entity: `Id` (`DefaultId`), `UserId` (`DefaultId?`), `EntityType`, `EntityId`, `Action` (`AuditAction`), `Changes` (JSON array of `AuditPropertyChange`), `CorrelationId`, `OccurredAt`. Persisted in `KernelDbContext.AuditEntries`.
- **`AuditLogSaveChangesInterceptor`** — Scoped `SaveChangesInterceptor` capturing entity mutations across all `DbContext`s (creates, updates with property diffs, deletes) and persisting `AuditEntry` records into `KernelDbContext`. Skips internal `AuditEntry` and `Message` entities to prevent recursion/noise.
- **`IAuditLogService` / `AuditLogService`** — Service for querying `AuditEntry` records with filtering (`EntityType`, `EntityId`, `UserId`, `Action`, date ranges, `CorrelationId`) and dynamic pagination/sorting via `QueryableExtensions`.
- **`GetAuditEntries` / `GetAuditEntriesHandler`** — CQRS query request extending `PaginatedQuery` and returning `PaginatedResult<AuditEntry>`.
- **`AuditController`** — Administrative endpoint at `api/v1/kernel/audit` protected by `[Authorize(Roles = Roles.System)]`.

> **Convention:** All entity mutations across all DbContexts are automatically captured by `AuditLogSaveChangesInterceptor`. Query audit records via `IAuditLogService` or `GET /api/v1/kernel/audit` (requires `System` role).

### Caching Abstraction (`Kernel/Caching/`)
A centralized caching abstraction supporting in-memory storage, dynamic configuration, programmatic cache access, and automatic request pipeline response caching:

- **`ICacheService`** — Abstraction for caching operations: `GetAsync<T>`, `SetAsync<T>`, `GetOrCreateAsync<T>`, `RemoveAsync`, `RemoveByPrefixAsync`.
- **`InMemoryCacheService`** — Default in-memory implementation backed by ASP.NET Core's `IMemoryCache`. Features active key tracking with post-eviction callbacks for prefix-based invalidation (`RemoveByPrefixAsync`) and key-level concurrency synchronization to prevent cache stampedes.
- **`CacheSettings`** — `DynamicSettingsSection` subclass (`SectionName => "Cache"`). Loaded from `appsettings.Cache.json` (`Enabled`, `DefaultExpirationSeconds`, `KeyPrefix`).
- **`[Cache]`** / **`CacheAttribute`** — Request interceptor attribute (`Order = -25`) for automatic handler response caching. Supports `DurationSeconds`, `KeyTemplate` (with `{Property}`, `{request.Property}`, `{userId}`, `{correlationId}` placeholders), `KeyPrefix`, and `VaryByUser`.
- **`CacheInterceptor`** — Request pipeline interceptor that resolves key templates dynamically, short-circuits execution on cache hits, and auto-persists successful `Result<TResponse>` values.
- **`AddCaching()`** — Registered automatically in `AddKernel()`.

> **Convention:** Decorate read/query request handlers with `[Cache]` to cache responses. Inject `ICacheService` when programmatic cache manipulation or cache invalidation (`RemoveAsync`, `RemoveByPrefixAsync`) is needed.

### Data Encryption (`Kernel/Encryption/`)
Column-level authenticated data encryption at rest using AES-256-GCM and transparent Entity Framework Core value converters:

- **`IEncryptionService`** — Interface for encrypting/decrypting strings (Base64 encoded payload) and raw byte arrays.
- **`AesEncryptionService`** — AES-256-GCM AEAD implementation. Automatically generates unique 12-byte random nonces per encryption, calculates 16-byte authentication tags to prevent tampering, and dynamically resolves keys from `EncryptionSettings`.
- **`EncryptionSettings`** — `DynamicSettingsSection` subclass (`SectionName => "Encryption"`). Loaded from `appsettings.Encryption.json`. Supports 32-byte Base64 keys and passphrase key derivation via SHA-256.
- **`[Encrypted]`** / **`EncryptedAttribute`** — Property attribute used on entity string and byte[] properties to mark them for transparent encryption at rest.
- **`EncryptedValueConverter`** / **`EncryptedBinaryValueConverter`** — EF Core `ValueConverter` implementations wrapping `IEncryptionService`.
- **`modelBuilder.UseEncryption(IEncryptionService)`** / **`modelBuilder.UseEncryption(DbContext)`** — Extension methods configuring EF Core entity models for transparent encryption of properties marked with `[Encrypted]`.

> **Convention:** Decorate sensitive entity properties (tokens, credentials, secrets) with `[Encrypted]` and call `modelBuilder.UseEncryption(this)` or `modelBuilder.UseEncryption(encryptionService)` in `OnModelCreating`. Inject `IEncryptionService` when explicit manual data encryption/decryption is required.

### API Versioning (`Kernel/Versioning/`)
A unified API versioning subsystem based on `Asp.Versioning.Http`, `Asp.Versioning.Mvc`, and `Asp.Versioning.Mvc.ApiExplorer`, configurable at runtime via `VersioningSettings` and `appsettings.Versioning.json`:

- **`VersioningSettings`** — `DynamicSettingsSection` subclass (`SectionName => "Versioning"`). Loaded from `appsettings.Versioning.json`. Exposes `DefaultVersion`, `AssumeDefaultVersionWhenUnspecified`, `ReportApiVersions`, `ApiVersionHeader`, `ApiVersionQueryParam`, `EnableUrlSegmentVersioning`, `EnableHeaderVersioning`, `EnableQueryVersioning`, `GetDefaultApiVersion()`, and `CreateApiVersionReader()`.
- **`VersioningExtensions`** — Extension methods: `builder.AddKernelVersioning()` / `services.AddKernelVersioning(settings)`.
- **`IModule.SupportedVersions`** — Default interface property (`static virtual IReadOnlyList<ApiVersion> SupportedVersions => [new ApiVersion(1, 0)];`).
- **`ApiVersioningAssertions`** (`Kernel.Testing/Assertions/`) — Fluent assertions (`ShouldHaveSupportedApiVersions()`, `ShouldHaveDeprecatedApiVersions()`) on `HttpResponseMessage`.

> **Convention:** Version controllers using `[ApiVersion(1.0)]` and `[Route("api/v{version:apiVersion}/...")]`. Minimal APIs can map versioned route groups using `app.NewApiVersionSet()`. Configure global default versioning behavior in `appsettings.Versioning.json`.

### File Storage (`Kernel/Storage/`)
A unified file and blob storage subsystem with a local filesystem provider, runtime-mutable settings, path traversal protection, size limits, and test fakes:

- **`IFileStorage`** — Abstraction for storing, retrieving, and deleting files and binary blobs (`UploadAsync`, `DownloadAsync`, `DeleteAsync`, `GetUrlAsync`, `ExistsAsync`).
- **`LocalFileStorage`** — Default filesystem provider. Features directory auto-creation, path traversal attack prevention (`../`, `..\`), file size limit enforcement, and error isolation with railway-oriented `Result`.
- **`FileStorageSettings`** — `DynamicSettingsSection` subclass (`SectionName => "Storage"`). Loaded from `appsettings.Storage.json`. Exposes `Provider`, `BasePath`, `BaseUrl`, `MaxFileSizeBytes`, `AllowedExtensions`, and `AutoCreateDirectory`.
- **`StorageExtensions`** (`Kernel/Storage/Extensions/`) — Extension methods: `UploadAsync(path, byte[], ...)`, `DownloadBytesAsync(path, ...)`, `UploadTextAsync(path, text, ...)`, `DownloadTextAsync(path, ...)`, and `AddFileStorage()`.
- **`FakeFileStorage`** (`Kernel.Testing/Fakes/`) — Thread-safe in-memory test fake tracking stored files, deleted paths, call counters (`UploadCount`, `DownloadCount`, `DeleteCount`), and failure simulation flags (`SimulateFailureOnUpload`, `SimulateFailureOnDownload`, `SimulateFailureOnDelete`).
- **`FileStorageAssertions`** (`Kernel.Testing/Assertions/`) — Fluent assertions: `storage.ShouldHaveFile(path)`, `storage.ShouldHaveFile(path, bytes)`, `storage.ShouldHaveFile(path, text)`, `storage.ShouldNotHaveFile(path)`, `storage.ShouldHaveDeleted(path)`, `storage.ShouldBeEmpty()`, `storage.ShouldHaveCount(count)`.

> **Convention:** Inject `IFileStorage` into services or handlers needing file persistence. Use extension methods (`UploadTextAsync`, `DownloadBytesAsync`) for convenient serialization. In tests, use `fixture.FileStorage` and assert with `fixture.FileStorage.ShouldHaveFile(...)`.

### Real-Time Notifications (`Kernel/Notifications/`)
A real-time notification abstraction backed by ASP.NET Core SignalR, configurable via dynamic settings, targeting individual users, named groups, or all connected clients:

- **`INotificationService`** — Abstraction for pushing real-time notifications:
  - `SendToUserAsync(userId, channel, payload, ct)` — Pushes notification to a specific user across all active connections.
  - `SendToGroupAsync(group, channel, payload, ct)` — Pushes notification to all connected clients in a named group.
  - `SendToAllAsync(channel, payload, ct)` — Broadcasts notification to all connected clients.
- **`SignalRNotificationService`** — Default SignalR implementation using `IHubContext<NotificationHub>`. Respects `NotificationSettings.Enabled` and invokes both generic `ReceiveNotification(channel, payload)` and named channel methods on clients.
- **`NotificationHub`** — SignalR `Hub` implementation providing `JoinGroup(group)` and `LeaveGroup(group)` client methods along with connection lifecycle logging.
- **`KernelUserIdProvider`** — `IUserIdProvider` implementation resolving user identifier from standard claims (`sub`, `ClaimTypes.NameIdentifier`, `ClaimTypes.Name`) aligned with `IScopeContext`.
- **`NotificationSettings`** — `DynamicSettingsSection` subclass (`SectionName => "Notifications"`). Loaded from `appsettings.Notifications.json` (`Enabled`, `Path`).
- **`NotificationExtensions`** (`Kernel/Notifications/Extensions/`) — Extension methods `services.AddNotifications()` and `app.MapNotifications()`.
- **`FakeNotificationService`** (`Kernel.Testing/Fakes/`) — In-memory test fake capturing sent notifications (`SentNotifications`, `GetSentToUser`, `GetSentToGroup`, `GetBroadcastNotifications`, `HasSentToUser`, `HasSentToGroup`, `HasSentBroadcast`, `Clear`).
- **`NotificationAssertions`** (`Kernel.Testing/Assertions/`) — Fluent assertions: `service.ShouldHaveSentToUser(userId)`, `service.ShouldHaveSentToGroup(group)`, `service.ShouldHaveSentBroadcast()`, `service.ShouldNotHaveSentNotifications()`, `service.ShouldHaveNotificationCount(count)`.

> **Convention:** Inject `INotificationService` into event handlers, workers, or controllers to push real-time updates to users or groups. In tests, use `fixture.NotificationService` and assert with `fixture.NotificationService.ShouldHaveSentToUser(...)`.

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
- **`KernelTestBase`** (`Kernel.Testing/Infrastructure/`) — Base test fixture providing an isolated, in-memory DI container, in-memory `KernelDbContext`, pre-configured fakes (`EventBus`, `ScopeContext`, `EmailSender`, `CacheService`, `EncryptionService`, `FileStorage`, `NotificationService`), `Dispatcher`, and scope execution helpers (`ExecuteScopeAsync`, `ExecuteDbContextAsync`).
- **`ModuleTestBase<TModule>` / `ModuleTestBase<TModule, TDbContext>`** (`Kernel.Testing/Infrastructure/`) — Extends `KernelTestBase` to automatically register a module's services, request handlers, event handlers, validators, authorization policies, and health checks, and pre-seeds declared roles and ensures module tables are created.
- **`TestWebApplicationFactory<TProgram>`** (`Kernel.Testing/Infrastructure/`) — Pre-configured `WebApplicationFactory` for full integration testing of the Host and active modules with an in-memory database and test fakes for `EventBus`, `EmailSender`, `CacheService`, `EncryptionService`, `FileStorage`, and `NotificationService`, plus a built-in test authentication handler (`CreateAuthenticatedClient(userId, roles)`).

> **Convention:** In test projects, reference `Kernel.Testing.csproj`. For unit/handler tests of a module, inherit from `ModuleTestBase<MyModule, MyDbContext>`. Use `Dispatcher.DispatchAsync(request)`, configure identity with `ScopeContext.AsSystem()` or `ScopeContext.AsAuthenticated()`, assert outcomes with `.ShouldBeSuccess()`, and assert event publications with `EventBus.ShouldHavePublished<MyEvent>()`. For full HTTP endpoint integration tests, use `TestWebApplicationFactory<Program>`.

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

This automatically scaffolds both `Modules/Orders` and `Modules/Orders.Contracts`, adds both to the solution under the `Modules` folder, and references `Modules/Orders` in `Host/Host.csproj`.

---

## Coding Conventions

### Naming & Structure
- **Identifiers**: Always `DefaultId` (= `System.Ulid`). Never use `Guid`, `int`, or `string` for entity IDs.
- **Features & Requests**: Place requests/commands/queries in `Features/` (or `{ModuleName}.Contracts/Requests/` if shared). Do **not** append `Request` to the class/record name; name them directly as verbs/actions (e.g., `GetOrders`, `CreateOrder`). The handler is named with a `Handler` suffix (e.g. `GetOrdersHandler`, `CreateOrderHandler`).
- **Endpoints & Routing**: All API endpoints must start with `api/<version>/` (e.g., `api/v1/orders`). Group endpoints using `.WithTags("{ModuleName}").WithGroupName("{ModuleName}")`.
- **Dependencies**: Modules depend on `Kernel` (and `Data` if using EF). Modules must never depend on other module implementations directly — reference the target module's companion `.Contracts` project and communicate via `IDispatcher` or `IEventBus`.
