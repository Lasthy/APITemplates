# MyModule — Module Agent Guide

This project is a self-contained **Modular Web API (MWA)** module. It encapsulates domain logic, API endpoints, background workers, event handlers, and data persistence for the `MyModule` feature area.

---

## Module Anatomy

```
Modules/MyModule/
├── Features/                              # CQRS requests & handlers (vertical slices)
│   ├── CreateItem/                        # Example feature slice
│   │   ├── CreateItem.cs                  # Command/Query record (no "Request" suffix)
│   │   └── CreateItemHandler.cs           # Handler returning Result<T>
├── Controllers/                           # Optional controller classes (if not using Minimal APIs)
├── Entities/                              # Domain entity definitions
├── Events/                                # Event definitions & event handlers
│   ├── ItemCreatedEvent.cs                # Event record inheriting from Event
│   └── ItemCreatedEventHandler.cs         # Implements IEventHandler<ItemCreatedEvent>
├── Workers/                               # Module background workers
│   └── MyModuleSyncWorker.cs              # Inherits BaseWorker (periodic or one-shot)
├── Configuration/                         # Optional custom DynamicSettingsSection
│   └── MyModuleSettings.cs                # Inherits DynamicSettingsSection
├── Persistence/                           # Module EF Core DbContext & configurations
├── MyModuleModule.cs                      # IModule entry point (Register, Policies, MapEndpoints)
├── settings.MyModule.json                 # Module dynamic settings (base)
├── settings.MyModule.Development.json     # Module dynamic settings (Dev overlay)
├── MyModule.csproj                        # Project references Kernel (and Data if using EF)
└── AGENTS.md                              # This module guide
```

---

## 1. Module Lifecycle (`MyModuleModule.cs`)

Every module implements `IModule`:

```csharp
public class MyModuleModule : IModule
{
    public static string Name => "MyModule";

    public static void RegisterPolicies(AuthorizationOptions options)
    {
        // Register module-specific authorization policies
        // options.AddPolicy("MyModuleAdmin", p => p.RequireRole("MyModuleAdmin"));
    }

    public static void Register(IServiceCollection services, IConfiguration configuration)
    {
        // Register module-specific services, repositories, validators, and DbContexts
        // services.AddScoped<IMyService, MyService>();
    }

    public static void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/mymodule")
            .WithTags("MyModule")
            .WithGroupName(Name);

        // Map module endpoints:
        // group.MapGet("/", async (...) => ...);
        // group.MapPost("/", async (...) => ...);
    }
}
```

### Module Activation
To enable this module, ensure `"MyModule": true` is set in `Host/settings.Instance.json` under `ActiveModules`:
```json
{
  "ActiveModules": {
    "MyModule": true
  }
}
```

---

## 2. API Endpoints & Routing

- **Route prefix**: All module routes must follow the pattern `api/v1/{module-name}` (e.g. `/api/v1/mymodule`).
- **OpenAPI grouping**: Always call `.WithTags(Name).WithGroupName(Name)` on route groups so endpoints appear under the module's dedicated OpenAPI document (`/openapi/MyModule.json`).
- **Authorization**: Inject `IScopeContext` for current identity details or use `.RequireAuthorization()`.

---

## 3. CQRS & Feature Organization

Organize business logic into vertical slices inside the `Features/` directory:

- **Requests / Commands / Queries**: Implement `IRequest<TResponse>`. Name them directly as verbs/actions (e.g., `CreateItem`, `GetItems`, `UpdateItemStatus`). **Do not** append `Request` to the class/record name.
- **Handlers**: Implement `IRequestHandler<TRequest, TResponse>`. Name them with the `Handler` suffix (e.g., `CreateItemHandler`, `GetItemsHandler`). Handlers are automatically discovered and registered by `AddModules()`.
- **Result pattern**: All handlers must return `Result<TResponse>`. Never throw expected exceptions.
- **Interceptors**: Decorate handler classes with attributes such as `[Logging]`, `[Transaction]`, or `[Intercept<TCustom>]` with optional `Order = n`.

**Example Feature:**
```csharp
// Features/CreateItem.cs
using Kernel.Requests;

public record CreateItem(string Name, decimal Price) : IRequest<DefaultId>;

// Features/CreateItemHandler.cs
using Kernel.Common;
using Kernel.Requests;

[Logging(Order = 1)]
[Transaction(Order = 2)]
public class CreateItemHandler : IRequestHandler<CreateItem, DefaultId>
{
    private readonly ILogger<CreateItemHandler> _logger;

    public CreateItemHandler(ILogger<CreateItemHandler> logger)
    {
        _logger = logger;
    }

    public async Task<Result<DefaultId>> HandleAsync(CreateItem command, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            return Result.Failure<DefaultId>(new ArgumentException("Item name cannot be empty."));

        var id = DefaultId.NewUlid();
        // ... save logic ...
        return Result.Success(id);
    }
}
```

---

## 4. Dynamic Configuration

Modules have their own `settings.MyModule.json` and optional `settings.MyModule.Development.json` overlays.

### Creating Strongly-Typed Module Settings
Inherit from `DynamicSettingsSection` — it will be auto-discovered and registered during module startup:

```csharp
public class MyModuleSettings : DynamicSettingsSection
{
    public override string SectionName => "MyModule";

    public int BatchSize => GetValue<int>(nameof(BatchSize));
    public string ApiKey => GetValue<string>(nameof(ApiKey));

    public MyModuleSettings(IJsonSerializer jsonSerializer, string? environment = null)
        : base("settings.MyModule.json", jsonSerializer, environment) { }
}
```

---

## 5. Background Workers (`BaseWorker`)

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
        // var dbContext = services.GetRequiredService<MyModuleDbContext>();
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

Register workers in `MyModuleModule.Register()`:
```csharp
services.AddHostedService<ItemSyncWorker>();
```

---

## 6. Events & Messaging

### Defining an Event
Inherit from `Event` (from `Kernel.Events`):
```csharp
public record ItemCreatedEvent(DefaultId ItemId, string Name) : Event;
```

### Handling an Event
Implement `IEventHandler<TEvent>`. Event handlers in the module assembly are auto-discovered during startup:
```csharp
public class ItemCreatedEventHandler(ILogger<ItemCreatedEventHandler> logger) : IEventHandler<ItemCreatedEvent>
{
    public Task HandleAsync(ItemCreatedEvent @event, CancellationToken ct = default)
    {
        logger.LogInformation("Item {ItemId} created: {Name}", @event.ItemId, @event.Name);
        return Task.CompletedTask;
    }
}
```

### Publishing an Event
Inject `IEventBus` and call `PublishAsync`:
```csharp
await eventBus.PublishAsync(new ItemCreatedEvent(itemId, name), ct);
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

1. **No direct module-to-module dependencies**: A module must never reference another module project. Communicate via events (`IEventBus`) or Kernel shared contracts.
2. **Never throw expected business exceptions**: Always return `Result.Failure(exception)` or `Result.Success(value)`.
3. **Use `IJsonSerializer`**: Never use `System.Text.Json.JsonSerializer` directly.
4. **Use `DefaultId`**: Never use `Guid`, `int`, or raw strings for entity IDs.
