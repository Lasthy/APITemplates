# Catalog — Module Agent Guide

This project is a self-contained **Modular Web API (MWA)** module. It encapsulates domain logic, API endpoints, background workers, event handlers, and data persistence for the `Catalog` feature area, paired with its companion public contracts library `Catalog.Contracts`.

---

## Module Anatomy

```
Modules/
├── Catalog/                                  # Module implementation
│   ├── Features/                              # CQRS requests & handlers (vertical slices)
│   │   ├── CreateItem/                        # Internal feature slice
│   │   │   ├── CreateItem.cs                  # Command/Query record
│   │   │   └── CreateItemHandler.cs           # Handler returning Result<T>
│   │   └── GetCatalogSummaryHandler.cs       # Implements IRequestHandler from Contracts
│   ├── Controllers/                           # Optional controller classes (if not using Minimal APIs)
│   ├── Entities/                              # Domain entity definitions
│   ├── Events/                                # Internal events & event handlers
│   │   └── ItemCreatedEventHandler.cs         # Implements IEventHandler<ItemCreatedEvent>
│   ├── Workers/                               # Module background workers
│   │   └── CatalogSyncWorker.cs              # Inherits BaseWorker (periodic or one-shot)
│   ├── Configuration/                         # Optional custom DynamicSettingsSection
│   │   └── CatalogSettings.cs                # Inherits DynamicSettingsSection
│   ├── Persistence/                           # Module EF Core DbContext & configurations
│   ├── CatalogModule.cs                      # IModule entry point (Register, Policies, MapEndpoints)
│   ├── appsettings.Catalog.json              # Module dynamic settings (base)
│   ├── appsettings.Catalog.Development.json  # Module dynamic settings (Dev overlay)
│   ├── Catalog.csproj                        # References Kernel & Catalog.Contracts
│   └── AGENTS.md                              # This module guide
│
└── Catalog.Contracts/                        # Companion public contracts (shareable)
    ├── Dtos/                                  # Public DTO records
    │   └── CatalogSummaryDto.cs
    ├── Events/                                # Public domain event records (inheriting Event)
    │   └── CatalogCreatedEvent.cs
    ├── Requests/                              # Public CQRS requests (implementing IRequest<T>)
    │   └── GetCatalogSummary.cs
    └── Catalog.Contracts.csproj              # References Kernel only
```

---

## 1. Module Lifecycle (`CatalogModule.cs`)

Every module implements `IModule`:

```csharp
public class CatalogModule : IModule
{
    public static string Name => "Catalog";

    public static void RegisterPolicies(AuthorizationOptions options)
    {
        // Register module-specific authorization policies
        // options.AddPolicy("CatalogAdmin", p => p.RequireRole("CatalogAdmin"));
    }

    public static void Register(IServiceCollection services, IConfiguration configuration)
    {
        // Register module-specific services, repositories, validators, and DbContexts
        // services.AddScoped<IMyService, MyService>();
    }

    public static void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/catalog")
            .WithTags("Catalog")
            .WithGroupName(Name);

        // Map module endpoints:
        // group.MapGet("/", async (...) => ...);
        // group.MapPost("/", async (...) => ...);
    }
}
```

### Module Activation
To enable this module, ensure `"Catalog": true` is set in `Host/appsettings.Instance.json` under `ActiveModules`:
```json
{
  "ActiveModules": {
    "Catalog": true
  }
}
```

---

## 2. Cross-Module Communication

### Rule: Companion Contracts Only
A module must **never** reference another module's implementation project. To interact with another module (e.g. `Orders`), reference its companion contracts library:

```xml
<!-- In AnotherModule.csproj -->
<ItemGroup>
  <ProjectReference Include="../Orders.Contracts/Orders.Contracts.csproj" />
</ItemGroup>
```

### Synchronous Queries/Commands (via `IDispatcher`)
1. In `Catalog.Contracts/Requests/GetCatalogSummary.cs`:
   ```csharp
   public record GetCatalogSummary(DefaultId Id) : IRequest<CatalogSummaryDto>;
   ```
2. In `Catalog/Features/GetCatalogSummaryHandler.cs`:
   ```csharp
   public class GetCatalogSummaryHandler : IRequestHandler<GetCatalogSummary, CatalogSummaryDto>
   {
       public async Task<Result<CatalogSummaryDto>> HandleAsync(GetCatalogSummary request, CancellationToken ct)
       {
           return Result.Success(new CatalogSummaryDto(request.Id, "Sample", true));
       }
   }
   ```
3. In another module (e.g. `Billing`), inject `IDispatcher`:
   ```csharp
   var result = await dispatcher.DispatchAsync(new GetCatalogSummary(id), ct);
   if (result.IsSuccess)
   {
       var summary = result.Value;
   }
   ```

### Asynchronous Pub/Sub Events (via `IEventBus`)
1. In `Catalog.Contracts/Events/CatalogCreatedEvent.cs`:
   ```csharp
   public record CatalogCreatedEvent(DefaultId Id, string Name) : Event;
   ```
2. In `Catalog`, publish when changes occur:
   ```csharp
   await eventBus.PublishAsync(new CatalogCreatedEvent(id, name), ct);
   ```
3. In another module, implement `IEventHandler<CatalogCreatedEvent>`:
   ```csharp
   public class OnCatalogCreatedHandler : IEventHandler<CatalogCreatedEvent>
   {
       public Task HandleAsync(CatalogCreatedEvent @event, CancellationToken ct = default)
       {
           // Handle event from Catalog
           return Task.CompletedTask;
       }
   }
   ```

---

## 3. API Endpoints & Routing

- **Route prefix**: All module routes must follow the pattern `api/v1/{module-name}` (e.g. `/api/v1/catalog`).
- **OpenAPI grouping**: Always call `.WithTags(Name).WithGroupName(Name)` on route groups so endpoints appear under the module's dedicated OpenAPI document (`/openapi/Catalog.json`).
- **Authorization**: Inject `IScopeContext` for current identity details or use `.RequireAuthorization()`.

---

## 4. CQRS & Feature Organization

Organize internal business logic into vertical slices inside the `Features/` directory:

- **Requests / Commands / Queries**: Implement `IRequest<TResponse>`. Name them directly as verbs/actions (e.g., `CreateItem`, `GetItems`, `UpdateItemStatus`). **Do not** append `Request` to the class/record name.
- **Handlers**: Implement `IRequestHandler<TRequest, TResponse>`. Name them with the `Handler` suffix (e.g., `CreateItemHandler`, `GetItemsHandler`). Handlers are automatically discovered and registered by `AddModules()`.
- **Result pattern**: All handlers must return `Result<TResponse>`. Never throw expected exceptions.
- **Interceptors**: Decorate handler classes with attributes such as `[Logging]`, `[Transaction]`, or `[Intercept<TCustom>]` with optional `Order = n`.

---

## 5. Dynamic Configuration

Modules have their own `appsettings.Catalog.json` and optional `appsettings.Catalog.Development.json` overlays.

### Creating Strongly-Typed Module Settings
Inherit from `DynamicSettingsSection` — it will be auto-discovered and registered during module startup:

```csharp
public class CatalogSettings : DynamicSettingsSection
{
    public override string SectionName => "Catalog";

    public int BatchSize => GetValue<int>(nameof(BatchSize));
    public string ApiKey => GetValue<string>(nameof(ApiKey));

    public CatalogSettings(IJsonSerializer jsonSerializer, string? environment = null)
        : base("appsettings.Catalog.json", jsonSerializer, environment) { }
}
```

---

## 6. Background Workers (`BaseWorker`)

Modules can run background workers by inheriting from `BaseWorker`:

### Periodic Worker (Runs on interval)
```csharp
public class ItemSyncWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<ItemSyncWorker> logger)
    : BaseWorker(scopeFactory, logger, TimeSpan.FromMinutes(5))
{
    protected override async Task ExecuteJobAsync(IServiceProvider services, CancellationToken ct)
    {
        // Scoped services can be resolved from 'services'
    }
}
```

### One-Shot Worker (Runs once at startup/job execution)
```csharp
public class ItemMigrationWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<ItemMigrationWorker> logger)
    : BaseWorker(scopeFactory, logger, isOneShot: true)
{
    protected override async Task ExecuteJobAsync(IServiceProvider services, CancellationToken ct)
    {
        // Executes once inside a DI scope and completes cleanly
    }
}
```

Register workers in `CatalogModule.Register()`:
```csharp
services.AddHostedService<ItemSyncWorker>();
```

---

## 7. Entities & Persistence

- **Identifiers**: Always use `DefaultId` (`System.Ulid`) for primary and foreign keys.
- **Marker Interfaces**:
  - `IAuditable` — `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy` (auto-populated by SaveChanges interceptors).
  - `ISoftDeletable` — `DeletedAt`, `DeletedBy` (auto-handled by soft-delete interceptors).
  - `IHasDomainEvents` — Exposes domain events dispatched automatically upon saving.

---

## 8. Rules & Best Practices

1. **No direct module-to-module dependencies**: A module must never reference another module project. Reference its companion `*.Contracts` project and communicate via `IDispatcher` or `IEventBus`.
2. **Never throw expected business exceptions**: Always return `Result.Failure(exception)` or `Result.Success(value)`.
3. **Use `IJsonSerializer`**: Never use `System.Text.Json.JsonSerializer` directly.
4. **Use `DefaultId`**: Never use `Guid`, `int`, or raw strings for entity IDs.
