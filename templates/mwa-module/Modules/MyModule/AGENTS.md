# MyModule — Module Agent Guide

This project is a self-contained **Modular Web API (MWA)** module. It encapsulates domain logic, API endpoints, background workers, event handlers, and data persistence for the `MyModule` feature area, paired with its companion public contracts library `MyModule.Contracts`.

---

## Module Anatomy

```
Modules/
├── MyModule/                                  # Module implementation
│   ├── Features/                              # CQRS requests & handlers (vertical slices)
│   │   ├── CreateItem/                        # Internal feature slice
│   │   │   ├── CreateItem.cs                  # Command/Query record
│   │   │   └── CreateItemHandler.cs           # Handler returning Result<T>
│   │   └── GetMyModuleSummaryHandler.cs       # Implements IRequestHandler from Contracts
│   ├── Controllers/                           # Optional controller classes (if not using Minimal APIs)
│   ├── Entities/                              # Domain entity definitions
│   ├── Events/                                # Internal events & event handlers
│   │   └── ItemCreatedEventHandler.cs         # Implements IEventHandler<ItemCreatedEvent>
│   ├── Workers/                               # Module background workers
│   │   └── MyModuleSyncWorker.cs              # Inherits BaseWorker (periodic or one-shot)
│   ├── Configuration/                         # Optional custom DynamicSettingsSection
│   │   └── MyModuleSettings.cs                # Inherits DynamicSettingsSection
│   ├── Persistence/                           # Module EF Core DbContext & configurations
│   ├── MyModuleModule.cs                      # IModule entry point (Register, Policies, MapEndpoints)
│   ├── appsettings.MyModule.json              # Module dynamic settings (base)
│   ├── appsettings.MyModule.Development.json  # Module dynamic settings (Dev overlay)
│   ├── MyModule.csproj                        # References Kernel & MyModule.Contracts
│   └── AGENTS.md                              # This module guide
│
└── MyModule.Contracts/                        # Companion public contracts (shareable)
    ├── Dtos/                                  # Public DTO records
    │   └── MyModuleSummaryDto.cs
    ├── Events/                                # Public domain event records (inheriting Event)
    │   └── MyModuleCreatedEvent.cs
    ├── Requests/                              # Public CQRS requests (implementing IRequest<T>)
    │   └── GetMyModuleSummary.cs
    └── MyModule.Contracts.csproj              # References Kernel only
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
To enable this module, ensure `"MyModule": true` is set in `Host/appsettings.Instance.json` under `ActiveModules`:
```json
{
  "ActiveModules": {
    "MyModule": true
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
1. In `MyModule.Contracts/Requests/GetMyModuleSummary.cs`:
   ```csharp
   public record GetMyModuleSummary(DefaultId Id) : IRequest<MyModuleSummaryDto>;
   ```
2. In `MyModule/Features/GetMyModuleSummaryHandler.cs`:
   ```csharp
   public class GetMyModuleSummaryHandler : IRequestHandler<GetMyModuleSummary, MyModuleSummaryDto>
   {
       public async Task<Result<MyModuleSummaryDto>> HandleAsync(GetMyModuleSummary request, CancellationToken ct)
       {
           return Result.Success(new MyModuleSummaryDto(request.Id, "Sample", true));
       }
   }
   ```
3. In another module (e.g. `Billing`), inject `IDispatcher`:
   ```csharp
   var result = await dispatcher.DispatchAsync(new GetMyModuleSummary(id), ct);
   if (result.IsSuccess)
   {
       var summary = result.Value;
   }
   ```

### Asynchronous Pub/Sub Events (via `IEventBus`)
1. In `MyModule.Contracts/Events/MyModuleCreatedEvent.cs`:
   ```csharp
   public record MyModuleCreatedEvent(DefaultId Id, string Name) : Event;
   ```
2. In `MyModule`, publish when changes occur:
   ```csharp
   await eventBus.PublishAsync(new MyModuleCreatedEvent(id, name), ct);
   ```
3. In another module, implement `IEventHandler<MyModuleCreatedEvent>`:
   ```csharp
   public class OnMyModuleCreatedHandler : IEventHandler<MyModuleCreatedEvent>
   {
       public Task HandleAsync(MyModuleCreatedEvent @event, CancellationToken ct = default)
       {
           // Handle event from MyModule
           return Task.CompletedTask;
       }
   }
   ```

---

## 3. API Endpoints & Routing

- **Route prefix**: All module routes must follow the pattern `api/v1/{module-name}` (e.g. `/api/v1/mymodule`).
- **OpenAPI grouping**: Always call `.WithTags(Name).WithGroupName(Name)` on route groups so endpoints appear under the module's dedicated OpenAPI document (`/openapi/MyModule.json`).
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

Modules have their own `appsettings.MyModule.json` and optional `appsettings.MyModule.Development.json` overlays.

### Creating Strongly-Typed Module Settings
Inherit from `DynamicSettingsSection` — it will be auto-discovered and registered during module startup:

```csharp
public class MyModuleSettings : DynamicSettingsSection
{
    public override string SectionName => "MyModule";

    public int BatchSize => GetValue<int>(nameof(BatchSize));
    public string ApiKey => GetValue<string>(nameof(ApiKey));

    public MyModuleSettings(IJsonSerializer jsonSerializer, string? environment = null)
        : base("appsettings.MyModule.json", jsonSerializer, environment) { }
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

Register workers in `MyModuleModule.Register()`:
```csharp
services.AddHostedService<ItemSyncWorker>();
```

---

## 7. Entities & Persistence

- **Module DbContext**: Every module includes its own `Persistence/MyModuleDbContext.cs` implementing `IDbContextSchema` (`Schema => "mymodule"`).
- **Schema Resolving**: Tables are isolated in their own database schema on supported providers (PostgreSQL, SQL Server) and automatically bypassed on SQLite/InMemory.
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
