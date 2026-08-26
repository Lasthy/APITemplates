# Orders — Module Agent Guide

This project is a self-contained **Modular Web API (MWA)** module. It encapsulates domain logic, API endpoints, background workers, event handlers, and data persistence for the `Orders` feature area, paired with its companion public contracts library `Orders.Contracts`.

---

## Module Anatomy

```
Modules/
├── Orders/                                  # Module implementation
│   ├── Features/                              # CQRS requests & handlers (vertical slices)
│   │   ├── CreateItem/                        # Internal feature slice
│   │   │   ├── CreateItem.cs                  # Command/Query record
│   │   │   └── CreateItemHandler.cs           # Handler returning Result<T>
│   │   └── GetOrdersSummaryHandler.cs       # Implements IRequestHandler from Contracts
│   ├── Controllers/                           # Optional controller classes (if not using Minimal APIs)
│   ├── Entities/                              # Domain entity definitions
│   ├── Events/                                # Internal events & event handlers
│   │   └── ItemCreatedEventHandler.cs         # Implements IEventHandler<ItemCreatedEvent>
│   ├── Workers/                               # Module background workers
│   │   └── OrdersSyncWorker.cs              # Inherits BaseWorker (periodic or one-shot)
│   ├── Configuration/                         # Optional custom DynamicSettingsSection
│   │   └── OrdersSettings.cs                # Inherits DynamicSettingsSection
│   ├── Persistence/                           # Module EF Core DbContext & configurations
│   ├── OrdersModule.cs                      # IModule entry point (Register, Policies, MapEndpoints)
│   ├── appsettings.Orders.json              # Module dynamic settings (base)
│   ├── appsettings.Orders.Development.json  # Module dynamic settings (Dev overlay)
│   ├── Orders.csproj                        # References Kernel & Orders.Contracts
│   └── AGENTS.md                              # This module guide
│
└── Orders.Contracts/                        # Companion public contracts (shareable)
    ├── Dtos/                                  # Public DTO records
    │   └── OrdersSummaryDto.cs
    ├── Events/                                # Public domain event records (inheriting Event)
    │   └── OrdersCreatedEvent.cs
    ├── Requests/                              # Public CQRS requests (implementing IRequest<T>)
    │   └── GetOrdersSummary.cs
    └── Orders.Contracts.csproj              # References Kernel only
```

---

## 1. Module Lifecycle (`OrdersModule.cs`)

Every module implements `IModule`:

```csharp
public class OrdersModule : IModule
{
    public static string Name => "Orders";

    public static void RegisterPolicies(AuthorizationOptions options)
    {
        // Register module-specific authorization policies
        // options.AddPolicy("OrdersAdmin", p => p.RequireRole("OrdersAdmin"));
    }

    public static void Register(IServiceCollection services, IConfiguration configuration)
    {
        // Register module-specific services, repositories, validators, and DbContexts
        // services.AddScoped<IMyService, MyService>();
    }

    public static void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/orders")
            .WithTags("Orders")
            .WithGroupName(Name);

        // Map module endpoints:
        // group.MapGet("/", async (...) => ...);
        // group.MapPost("/", async (...) => ...);
    }
}
```

### Module Activation
To enable this module, ensure `"Orders": true` is set in `Host/appsettings.Instance.json` under `ActiveModules`:
```json
{
  "ActiveModules": {
    "Orders": true
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
1. In `Orders.Contracts/Requests/GetOrdersSummary.cs`:
   ```csharp
   public record GetOrdersSummary(DefaultId Id) : IRequest<OrdersSummaryDto>;
   ```
2. In `Orders/Features/GetOrdersSummaryHandler.cs`:
   ```csharp
   public class GetOrdersSummaryHandler : IRequestHandler<GetOrdersSummary, OrdersSummaryDto>
   {
       public async Task<Result<OrdersSummaryDto>> HandleAsync(GetOrdersSummary request, CancellationToken ct)
       {
           return Result.Success(new OrdersSummaryDto(request.Id, "Sample", true));
       }
   }
   ```
3. In another module (e.g. `Billing`), inject `IDispatcher`:
   ```csharp
   var result = await dispatcher.DispatchAsync(new GetOrdersSummary(id), ct);
   if (result.IsSuccess)
   {
       var summary = result.Value;
   }
   ```

### Asynchronous Pub/Sub Events (via `IEventBus`)
1. In `Orders.Contracts/Events/OrdersCreatedEvent.cs`:
   ```csharp
   public record OrdersCreatedEvent(DefaultId Id, string Name) : Event;
   ```
2. In `Orders`, publish when changes occur:
   ```csharp
   await eventBus.PublishAsync(new OrdersCreatedEvent(id, name), ct);
   ```
3. In another module, implement `IEventHandler<OrdersCreatedEvent>`:
   ```csharp
   public class OnOrdersCreatedHandler : IEventHandler<OrdersCreatedEvent>
   {
       public Task HandleAsync(OrdersCreatedEvent @event, CancellationToken ct = default)
       {
           // Handle event from Orders
           return Task.CompletedTask;
       }
   }
   ```

---

## 3. API Endpoints & Routing

- **Route prefix**: All module routes must follow the pattern `api/v1/{module-name}` (e.g. `/api/v1/orders`).
- **OpenAPI grouping**: Always call `.WithTags(Name).WithGroupName(Name)` on route groups so endpoints appear under the module's dedicated OpenAPI document (`/openapi/Orders.json`).
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

Modules have their own `appsettings.Orders.json` and optional `appsettings.Orders.Development.json` overlays.

### Creating Strongly-Typed Module Settings
Inherit from `DynamicSettingsSection` — it will be auto-discovered and registered during module startup:

```csharp
public class OrdersSettings : DynamicSettingsSection
{
    public override string SectionName => "Orders";

    public int BatchSize => GetValue<int>(nameof(BatchSize));
    public string ApiKey => GetValue<string>(nameof(ApiKey));

    public OrdersSettings(IJsonSerializer jsonSerializer, string? environment = null)
        : base("appsettings.Orders.json", jsonSerializer, environment) { }
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

Register workers in `OrdersModule.Register()`:
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
