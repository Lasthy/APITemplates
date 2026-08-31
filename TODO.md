# APITemplates — Feature TODO & Specifications

> Master specification for all planned features. Each section is a self-contained spec
> ready for implementation. Features are ordered by priority.

---

## Table of Contents

| # | Feature | Priority | Location |
|---|---|---|---|
| 1 | [Authorization Policies](#1--authorization-policies) | 🔴 Critical | `Kernel/Authorization/` |
| 2 | [Rate Limiting](#2--rate-limiting) | 🔴 Critical | `Kernel/RateLimiting/` |
| 3 | [Email Channel](#3--email-channel) | 🔴 Critical | `Kernel/Messaging/`, `Kernel/Email/` |
| 4 | [Validation Interceptor](#4--validation-interceptor) | 🟠 High | `Kernel/Validation/` |
| 5 | [Pagination & Filtering](#5--pagination--filtering) | 🟠 High | `Kernel/Common/` |
| 6 | [Observability / Diagnostics](#6--observability--diagnostics) | 🟠 High | `Kernel/Diagnostics/` |
| 7 | [Structured Audit Log](#7--structured-audit-log) | 🟡 Medium | `Kernel/AuditLog/` |
| 8 | [Caching Abstraction](#8--caching-abstraction) | 🟡 Medium | `Kernel/Caching/` |
| 9 | [Inbox / Idempotency](#9--inbox--idempotency) | 🟡 Medium | `Kernel/Messaging/` |
| 10 | [Testing Utilities](#10--testing-utilities) | 🟡 Medium | `Kernel.Testing/` (new project) |
| 11 | [Data Encryption](#11--data-encryption) | 🟢 Lower | `Kernel/Encryption/` |
| 12 | [API Versioning](#12--api-versioning) | 🟢 Lower | `Kernel/Versioning/` |
| 13 | [File Storage](#13--file-storage) | 🟢 Lower | `Kernel/Storage/` |
| 14 | [Real-Time Notifications](#14--real-time-notifications) | 🟢 Lower | `Kernel/Notifications/` |
| 15 | [API Key Authentication](#15--api-key-authentication) | 🟢 Lower | `mwa-module-identity` |

---

## 1. 🔐 Authorization Policies

**Priority:** 🔴 Critical  
**Location:** `Kernel/Authorization/`

### Motivation

Authorization currently lives only at the HTTP layer (ASP.NET `[Authorize]` attributes on controllers). Internally dispatched requests — from event handlers, workers, or cross-module calls — bypass these checks entirely. We need a policy-based authorization system that operates at the **dispatcher level** and also integrates with **API endpoints**, supporting custom authorization logic beyond simple roles/claims.

### Design Overview

An `IAuthorizationPolicy` interface with two methods:
1. `PolicyName` — identifies the policy.
2. `AuthorizeAsync(IScopeContext, CancellationToken)` — performs the actual authorization check, returning a `Result`. This allows custom logic: querying the database, checking resource ownership, validating subscription tiers, etc.

Policies are registered in DI and discovered by name. An `[Authorize("PolicyName")]` attribute on request handlers invokes the matching policy before the handler runs. For API endpoints, an `AuthorizationEndpointFilter` (or MVC filter) performs the same lookup, enabling one policy definition to protect both dispatcher requests and HTTP routes.

### File Inventory

```
Kernel/Authorization/
├── IAuthorizationPolicy.cs              [NEW] — Policy interface
├── AuthorizationPolicyRegistry.cs       [NEW] — Registry of named policies
├── AuthorizationInterceptor.cs          [NEW] — Request pipeline interceptor
├── AuthorizeAttribute.cs                [NEW] — Handler attribute
├── AuthorizationEndpointFilter.cs       [NEW] — IEndpointFilter for minimal APIs
├── AuthorizationActionFilter.cs         [NEW] — IActionFilter for MVC controllers
└── Extensions/
    └── AuthorizationExtensions.cs       [NEW] — DI registration helpers
```

### Interface & Class Shapes

```csharp
// ── IAuthorizationPolicy ──────────────────────────────────────────────────────
namespace Kernel.Authorization;

/// <summary>
/// A named authorization policy with custom authorization logic.
/// Implement this interface to define policies based on database lookups,
/// resource ownership, subscription tiers, or any business rule.
/// </summary>
public interface IAuthorizationPolicy
{
    /// <summary>
    /// Unique name identifying this policy. Referenced by [Authorize("name")] attributes.
    /// </summary>
    string PolicyName { get; }

    /// <summary>
    /// Evaluates whether the current scope context is authorized.
    /// Return Result.Success() to allow, Result.Failure(...) to deny.
    /// The request object is provided for resource-level authorization.
    /// </summary>
    Task<Result> AuthorizeAsync(
        IScopeContext context,
        object? resource,
        CancellationToken cancellationToken = default);
}
```

```csharp
// ── Built-in policies ─────────────────────────────────────────────────────────

/// Policy that checks if the user is authenticated.
public class AuthenticatedPolicy : IAuthorizationPolicy
{
    public string PolicyName => "Authenticated";

    public Task<Result> AuthorizeAsync(IScopeContext context, object? resource, CancellationToken ct)
    {
        return Task.FromResult(context.IsAuthenticated
            ? Result.Success()
            : Result.Failure(new UnauthorizedAccessException("Authentication required.")));
    }
}

/// Policy that checks for specific roles. Reusable by creating instances with different roles.
public class RolePolicy : IAuthorizationPolicy
{
    private readonly string[] _roles;
    public string PolicyName { get; }

    public RolePolicy(string policyName, params string[] roles)
    {
        PolicyName = policyName;
        _roles = roles;
    }

    public Task<Result> AuthorizeAsync(IScopeContext context, object? resource, CancellationToken ct)
    {
        if (!context.IsAuthenticated)
            return Task.FromResult(Result.Failure(new UnauthorizedAccessException("Authentication required.")));

        var hasRole = _roles.Any(r => context.IsInRole(r));
        return Task.FromResult(hasRole
            ? Result.Success()
            : Result.Failure(new UnauthorizedAccessException($"Required role: {string.Join(", ", _roles)}")));
    }
}
```

```csharp
// ── AuthorizationPolicyRegistry ───────────────────────────────────────────────

/// <summary>
/// Singleton registry of all named authorization policies.
/// Policies are auto-discovered from DI or registered manually.
/// </summary>
public class AuthorizationPolicyRegistry
{
    private readonly Dictionary<string, IAuthorizationPolicy> _policies = new(StringComparer.OrdinalIgnoreCase);

    public void Register(IAuthorizationPolicy policy) { ... }
    public IAuthorizationPolicy? GetPolicy(string name) { ... }
    public IReadOnlyCollection<string> GetPolicyNames() { ... }
}
```

```csharp
// ── AuthorizeAttribute (for request handlers) ─────────────────────────────────

/// <summary>
/// Applies authorization to a request handler. The dispatcher will resolve
/// the named IAuthorizationPolicy and call AuthorizeAsync before the handler runs.
/// Multiple policies can be applied (all must pass).
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public class AuthorizeAttribute : RequestInterceptorAttribute
{
    public string PolicyName { get; }

    public AuthorizeAttribute(string policyName)
    {
        PolicyName = policyName;
        Order = -100; // Run before logging/transaction interceptors
    }

    public override IRequestInterceptor CreateInterceptor(IServiceProvider serviceProvider)
    {
        var registry = serviceProvider.GetRequiredService<AuthorizationPolicyRegistry>();
        var scopeContext = serviceProvider.GetRequiredService<IScopeContext>();
        return new AuthorizationInterceptor(registry, scopeContext, PolicyName);
    }
}
```

```csharp
// ── AuthorizationInterceptor ──────────────────────────────────────────────────

public class AuthorizationInterceptor : IRequestInterceptor
{
    private readonly AuthorizationPolicyRegistry _registry;
    private readonly IScopeContext _scopeContext;
    private readonly string _policyName;

    public AuthorizationInterceptor(
        AuthorizationPolicyRegistry registry,
        IScopeContext scopeContext,
        string policyName) { ... }

    public async Task<Result<TResponse>> InterceptAsync<TResponse>(
        IRequest<TResponse> request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken = default)
    {
        var policy = _registry.GetPolicy(_policyName);
        if (policy == null)
            return Result.Failure<TResponse>(
                new InvalidOperationException($"Authorization policy '{_policyName}' not found."));

        var authResult = await policy.AuthorizeAsync(_scopeContext, request, cancellationToken);
        if (authResult.IsFailure)
            return Result.Failure<TResponse>(authResult.Error!);

        return await next();
    }
}
```

```csharp
// ── AuthorizationEndpointFilter (for minimal API endpoints) ───────────────────

/// <summary>
/// IEndpointFilter that evaluates a named IAuthorizationPolicy.
/// Use with .AddEndpointFilter() on minimal API route groups.
/// </summary>
public class AuthorizationEndpointFilter : IEndpointFilter
{
    private readonly string _policyName;

    public AuthorizationEndpointFilter(string policyName) { ... }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var registry = context.HttpContext.RequestServices.GetRequiredService<AuthorizationPolicyRegistry>();
        var scopeContext = context.HttpContext.RequestServices.GetRequiredService<IScopeContext>();
        var policy = registry.GetPolicy(_policyName);
        // ... evaluate, return 401/403 or continue
    }
}

// ── AuthorizationActionFilter (for MVC controllers) ───────────────────────────

/// <summary>
/// IActionFilter attribute for controllers. Evaluates a named IAuthorizationPolicy
/// before the action executes. Works alongside ASP.NET's built-in [Authorize].
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class AuthorizePolicyAttribute : ActionFilterAttribute
{
    public string PolicyName { get; }
    public AuthorizePolicyAttribute(string policyName) { ... }
    // OnActionExecutionAsync resolves policy from DI, checks IScopeContext, short-circuits with 403
}
```

### Usage Examples

```csharp
// ── Custom policy (e.g., resource ownership) ──────────────────────────────────
public class OwnsResourcePolicy : IAuthorizationPolicy
{
    public string PolicyName => "OwnsResource";
    private readonly IDbContext _db;

    public OwnsResourcePolicy(IDbContext db) { _db = db; }

    public async Task<Result> AuthorizeAsync(IScopeContext ctx, object? resource, CancellationToken ct)
    {
        if (ctx.IsSystem) return Result.Success(); // System bypasses

        // Extract entity ID from the request object
        if (resource is IHasEntityId req)
        {
            var entity = await _db.Set<OwnedEntity>().FindAsync(req.EntityId, ct);
            if (entity?.OwnerId == ctx.UserId)
                return Result.Success();
        }
        return Result.Failure(new UnauthorizedAccessException("You do not own this resource."));
    }
}

// ── On a request handler ──────────────────────────────────────────────────────
[Authorize("OwnsResource")]
[Transaction]
public class UpdateOrderHandler : IRequestHandler<UpdateOrder, OrderDto> { ... }

// ── On a minimal API endpoint ─────────────────────────────────────────────────
app.MapGet("/api/v1/orders/{id}", handler)
   .AddEndpointFilter(new AuthorizationEndpointFilter("OwnsResource"));

// ── On a controller ───────────────────────────────────────────────────────────
[AuthorizePolicy("Admin")]
[HttpDelete("{id}")]
public async Task<IActionResult> Delete(DefaultId id, CancellationToken ct) { ... }
```

### Integration Points

| Existing Component | Change |
|---|---|
| [`AddKernel()`](file:///d:/Dev/APITemplates/templates/mwa-solution/Kernel/Extensions/WebApplicationBuilderExtensions.cs#L39-L134) | Register `AuthorizationPolicyRegistry`, `AuthenticatedPolicy`, and scan for `IAuthorizationPolicy` implementations |
| [`AddModule<T>()`](file:///d:/Dev/APITemplates/templates/mwa-solution/Kernel/Modules/WebApplicationBuilderExtension.cs#L115-L187) | Auto-discover and register `IAuthorizationPolicy` implementations from module assemblies |
| [`Dispatcher`](file:///d:/Dev/APITemplates/templates/mwa-solution/Kernel/Requests/Dispatcher.cs) | No changes needed — `AuthorizeAttribute` extends existing `RequestInterceptorAttribute`, pipeline handles it automatically |
| [`IModule`](file:///d:/Dev/APITemplates/templates/mwa-solution/Kernel/Modules/IModule.cs) | No changes — `RegisterPolicies()` already exists and can register ASP.NET policies alongside the new system |

---

## 2. ⏱️ Rate Limiting

**Priority:** 🔴 Critical  
**Location:** `Kernel/RateLimiting/`

### Motivation

The templates have zero rate limiting. Auth endpoints (`login`, `forgot-password`, `refresh`) are fully exposed to brute-force attacks. Any API deployed from these templates is immediately vulnerable to resource exhaustion and credential stuffing.

### Design Overview

Leverage ASP.NET Core's built-in `System.Threading.RateLimiting` and `Microsoft.AspNetCore.RateLimiting` middleware, configured via a `RateLimitingSettings` dynamic settings section. Provide per-endpoint, per-user, and per-IP policies, all adjustable at runtime via `appsettings.RateLimiting.json`.

### File Inventory

```
Kernel/RateLimiting/
├── RateLimitingSettings.cs        [NEW] — DynamicSettingsSection for rate limit config
└── Extensions/
    └── RateLimitingExtensions.cs   [NEW] — builder.AddRateLimiting() + app.UseRateLimiting()

Host/
├── appsettings.RateLimiting.json  [NEW] — Default rate limit configuration
```

### Configuration Shape

```json
// appsettings.RateLimiting.json
{
    "Enabled": true,
    "GlobalPolicy": {
        "PermitLimit": 200,
        "WindowSeconds": 60,
        "QueueLimit": 0
    },
    "AuthenticatedPolicy": {
        "PermitLimit": 500,
        "WindowSeconds": 60
    },
    "EndpointOverrides": {
        "auth/login": { "PermitLimit": 5, "WindowSeconds": 60 },
        "auth/forgot-password": { "PermitLimit": 3, "WindowSeconds": 300 },
        "auth/refresh": { "PermitLimit": 10, "WindowSeconds": 60 }
    },
    "RejectionStatusCode": 429,
    "RejectionMessage": "Too many requests. Please try again later."
}
```

### Class Shapes

```csharp
public class RateLimitingSettings : DynamicSettingsSection
{
    public override string SectionName => "RateLimiting";

    public bool Enabled => GetValue<bool>(nameof(Enabled));
    public int GlobalPermitLimit => GetValue<int>("GlobalPolicy:PermitLimit");
    public int GlobalWindowSeconds => GetValue<int>("GlobalPolicy:WindowSeconds");
    public int RejectionStatusCode => GetValue<int>(nameof(RejectionStatusCode));
    // ... EndpointOverrides parsed as Dictionary<string, RateLimitEndpointConfig>

    public RateLimitingSettings(IJsonSerializer jsonSerializer, string? environment = null)
        : base("appsettings.RateLimiting.json", jsonSerializer, environment) { }
}
```

```csharp
// Extension methods
public static class RateLimitingExtensions
{
    public static void AddRateLimiting(this WebApplicationBuilder builder) { ... }
    public static void UseRateLimiting(this WebApplication app) { ... }
}
```

### Integration Points

| Existing Component | Change |
|---|---|
| [`AddKernel()`](file:///d:/Dev/APITemplates/templates/mwa-solution/Kernel/Extensions/WebApplicationBuilderExtensions.cs#L39-L134) | Call `builder.AddRateLimiting()` to register settings + policies |
| [`UseKernel()`](file:///d:/Dev/APITemplates/templates/mwa-solution/Kernel/Extensions/WebApplicationBuilderExtensions.cs#L190-L201) | Call `app.UseRateLimiting()` before `app.MapControllers()` |
| [`Program.cs`](file:///d:/Dev/APITemplates/templates/mwa-solution/Host/Program.cs) | No changes needed (auto-integrated via `AddKernel`/`UseKernel`) |

---

## 3. 📨 Email Channel

**Priority:** 🔴 Critical  
**Location:** `Kernel/Email/`, `Kernel/Messaging/`

### Motivation

[`MessageChannel.Email`](file:///d:/Dev/APITemplates/templates/mwa-solution/Kernel/Messaging/MessageChannel.cs#L16) exists as an enum value but has no processor. The [`MessageDispatcher`](file:///d:/Dev/APITemplates/templates/mwa-solution/Kernel/Messaging/MessageDispatcher.cs#L25-L31) logs an error and drops any email-channel message. The Identity module publishes events like `PasswordResetRequestedEvent` that should trigger emails.

### Design Overview

1. `IEmailSender` abstraction — simple send interface.
2. `SmtpEmailSender` — default SMTP implementation.
3. `EmailMessage` — strongly-typed email model (to, subject, body, html flag).
4. `EmailMessageProcessor` — implements `IMessageProcessor` for `MessageChannel.Email`.
5. `EmailSettings` — DynamicSettingsSection for SMTP configuration.
6. `IEmailTemplateRenderer` — optional abstraction for rendering email templates from embedded resources or file paths.

### File Inventory

```
Kernel/Email/
├── IEmailSender.cs                [NEW]
├── SmtpEmailSender.cs             [NEW]
├── EmailMessage.cs                [NEW]
├── EmailSettings.cs               [NEW] — DynamicSettingsSection
├── IEmailTemplateRenderer.cs      [NEW] — Optional template rendering
└── DefaultEmailTemplateRenderer.cs [NEW] — Simple string replacement renderer

Kernel/Messaging/
├── EmailMessageProcessor.cs       [NEW] — IMessageProcessor for MessageChannel.Email

Host/
├── appsettings.Email.json         [NEW]
```

### Interface Shapes

```csharp
public class EmailMessage
{
    public required string To { get; init; }
    public string? Cc { get; init; }
    public string? Bcc { get; init; }
    public required string Subject { get; init; }
    public required string Body { get; init; }
    public bool IsHtml { get; init; } = true;
}

public interface IEmailSender
{
    Task<Result> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

public interface IEmailTemplateRenderer
{
    Task<string> RenderAsync(string templateName, Dictionary<string, string> variables, CancellationToken ct = default);
}
```

```json
// appsettings.Email.json
{
    "SmtpHost": "smtp.example.com",
    "SmtpPort": 587,
    "SmtpUsername": "",
    "SmtpPassword": "",
    "EnableSsl": true,
    "FromAddress": "noreply@example.com",
    "FromName": "MyAPI"
}
```

### Integration Points

| Existing Component | Change |
|---|---|
| [`AddKernel()`](file:///d:/Dev/APITemplates/templates/mwa-solution/Kernel/Extensions/WebApplicationBuilderExtensions.cs#L39-L134) | Register `IEmailSender`, `EmailMessageProcessor`, `EmailSettings` |
| [`MessageDispatcher`](file:///d:/Dev/APITemplates/templates/mwa-solution/Kernel/Messaging/MessageDispatcher.cs) | No changes — it auto-discovers `IMessageProcessor` by channel |
| [`MessageChannel`](file:///d:/Dev/APITemplates/templates/mwa-solution/Kernel/Messaging/MessageChannel.cs) | No changes — `Email` already exists |

---

## 4. ✅ Validation Interceptor

**Priority:** 🟠 High  
**Location:** `Kernel/Validation/`

### Motivation

Every handler currently validates its own input manually. There's no standardized way to declare validation rules, no consistent error shape for validation failures, and validators aren't auto-discovered. A dispatcher-level validation interceptor would short-circuit with structured errors before the handler executes.

### Design Overview

An `IValidator<T>` interface with a `Validate(T)` method returning a `ValidationResult`. A `[Validate]` attribute on handlers that auto-resolves the matching validator. Validators are auto-discovered from module assemblies during `AddModule<T>()`.

### File Inventory

```
Kernel/Validation/
├── IValidator.cs                  [NEW] — IValidator<T> interface
├── ValidationResult.cs            [NEW] — Structured validation errors
├── ValidationException.cs         [NEW] — Exception type for validation failures
├── ValidateAttribute.cs           [NEW] — RequestInterceptorAttribute
├── ValidationInterceptor.cs       [NEW] — IRequestInterceptor implementation
└── Extensions/
    └── ValidationExtensions.cs    [NEW] — DI registration + auto-discovery
```

### Interface Shapes

```csharp
/// <summary>
/// Represents a single validation error on a specific field.
/// </summary>
public record ValidationError(string Field, string Message);

/// <summary>
/// Structured validation result containing all field errors.
/// </summary>
public class ValidationResult
{
    public IReadOnlyList<ValidationError> Errors { get; }
    public bool IsValid => Errors.Count == 0;

    public static ValidationResult Success() => new([]);
    public static ValidationResult Failure(params ValidationError[] errors) => new(errors);
    public static ValidationResult Failure(string field, string message) => new([new(field, message)]);
}

/// <summary>
/// Validates a request of type T before handler execution.
/// Auto-discovered from module assemblies during AddModule.
/// </summary>
public interface IValidator<in T>
{
    ValidationResult Validate(T request);
}
```

```csharp
/// <summary>
/// Attribute that triggers validation before handler execution.
/// Auto-resolves IValidator<TRequest> from DI.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class ValidateAttribute : RequestInterceptorAttribute
{
    public ValidateAttribute() { Order = -50; } // After auth, before logging

    public override IRequestInterceptor CreateInterceptor(IServiceProvider serviceProvider)
        => new ValidationInterceptor(serviceProvider);
}
```

```csharp
// ── Usage ─────────────────────────────────────────────────────────────────────
public class CreateUserValidator : IValidator<CreateUser>
{
    public ValidationResult Validate(CreateUser request)
    {
        var errors = new List<ValidationError>();
        if (string.IsNullOrWhiteSpace(request.Email))
            errors.Add(new("Email", "Email is required."));
        if (request.Password.Length < 8)
            errors.Add(new("Password", "Password must be at least 8 characters."));
        return errors.Count > 0 ? ValidationResult.Failure(errors.ToArray()) : ValidationResult.Success();
    }
}

[Validate]
[Transaction]
public class CreateUserHandler : IRequestHandler<CreateUser, UserDto> { ... }
```

### Integration Points

| Existing Component | Change |
|---|---|
| [`AddModule<T>()`](file:///d:/Dev/APITemplates/templates/mwa-solution/Kernel/Modules/WebApplicationBuilderExtension.cs#L115-L187) | Add auto-scan for `IValidator<>` implementations (same pattern as `RegisterEventHandlers`) |
| [`Dispatcher`](file:///d:/Dev/APITemplates/templates/mwa-solution/Kernel/Requests/Dispatcher.cs) | No changes — `ValidateAttribute` extends `RequestInterceptorAttribute` |

---

## 5. 📋 Pagination & Filtering

**Priority:** 🟠 High  
**Location:** `Kernel/Common/`

### Motivation

[`PagedResult<T>`](file:///d:/Dev/APITemplates/templates/mwa-solution/Kernel/Common/PagedResult.cs) exists as an output wrapper but there's no input abstraction. Every list handler re-implements skip/take/ordering. A shared `PaginatedQuery` base and `IQueryable` extensions would eliminate boilerplate.

### Design Overview

A `PaginatedQuery` base record for input, `IQueryable<T>` extension methods for applying pagination and sorting, and factory methods on `PagedResult<T>` for easy construction.

### File Inventory

```
Kernel/Common/
├── PaginatedQuery.cs              [NEW] — Base input record
├── SortDirection.cs               [NEW] — Enum (Ascending/Descending)
├── QueryableExtensions.cs         [NEW] — ApplyPagination, ApplySort, ToPagedResultAsync
├── PagedResult.cs                 [MODIFY] — Add factory methods
```

### Interface Shapes

```csharp
public enum SortDirection { Ascending, Descending }

/// <summary>
/// Base record for paginated queries. Handlers can extend this with filters.
/// </summary>
public record PaginatedQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? SortBy { get; init; }
    public SortDirection SortDirection { get; init; } = SortDirection.Ascending;

    /// Maximum allowed page size to prevent abuse. Override per-query if needed.
    public virtual int MaxPageSize => 100;
}
```

```csharp
public static class QueryableExtensions
{
    /// <summary>
    /// Applies sorting (by property name) and pagination (skip/take) to a queryable.
    /// </summary>
    public static IQueryable<T> ApplyPagination<T>(this IQueryable<T> query, PaginatedQuery pagination) { ... }

    /// <summary>
    /// Materializes the query and wraps in a PagedResult with total count.
    /// </summary>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        PaginatedQuery pagination,
        CancellationToken ct = default) { ... }
}
```

```csharp
// ── Usage ─────────────────────────────────────────────────────────────────────
public record GetUsers(string? Search = null) : PaginatedQuery, IRequest<IEnumerable<UserDto>>;

// In handler:
var result = await dbContext.Users
    .Where(u => search == null || u.UserName.Contains(search))
    .ApplyPagination(request)
    .Select(u => new UserDto(...))
    .ToPagedResultAsync(request, ct);
```

### Changes to PagedResult

```csharp
// Add to PagedResult<T>:
public int TotalCount { get; }
public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
public bool HasNextPage => Page < TotalPages;
public bool HasPreviousPage => Page > 1;

public static PagedResult<T> Create(IEnumerable<T> items, int page, int pageSize, int totalCount) { ... }
```

---

## 6. 📈 Observability / Diagnostics

**Priority:** 🟠 High  
**Location:** `Kernel/Diagnostics/`

### Motivation

The only observability is the opt-in [`[Logging]`](file:///d:/Dev/APITemplates/templates/mwa-solution/Kernel/Requests/LoggingInterceptor.cs) handler interceptor. There's no correlation ID propagation, no HTTP-level request diagnostics, no way to trace a request through the event/outbox pipeline.

### Design Overview

1. `CorrelationIdMiddleware` — generates or extracts `X-Correlation-Id`, stores in `IScopeContext` and `Activity.Current`.
2. `RequestDiagnosticsMiddleware` — logs HTTP method, path, status code, duration, user ID.
3. Correlation ID flows into outbox `Message` entity for end-to-end tracing.
4. Optional OpenTelemetry integration points (Activity sources).

### File Inventory

```
Kernel/Diagnostics/
├── CorrelationIdMiddleware.cs      [NEW]
├── RequestDiagnosticsMiddleware.cs [NEW]
├── DiagnosticsConstants.cs         [NEW] — Header name, Activity source name
└── Extensions/
    └── DiagnosticsExtensions.cs    [NEW] — app.UseCorrelationId(), app.UseRequestDiagnostics()

Kernel/Context/
├── IScopeContext.cs               [MODIFY] — Add CorrelationId property
├── WebScopeContext.cs             [MODIFY] — Read from HttpContext.Items or header

Kernel/Messaging/
├── Message.cs                     [MODIFY] — Add CorrelationId column
├── MessageWriter.cs               [MODIFY] — Set CorrelationId from IScopeContext
```

### Class Shapes

```csharp
public class CorrelationIdMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault()
            ?? DefaultId.NewUlid().ToString();

        context.Items["CorrelationId"] = correlationId;
        context.Response.Headers["X-Correlation-Id"] = correlationId;

        using var activity = DiagnosticsConstants.ActivitySource.StartActivity("HttpRequest");
        activity?.SetTag("correlation.id", correlationId);

        await next(context);
    }
}

public class RequestDiagnosticsMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var sw = Stopwatch.StartNew();
        try { await next(context); }
        finally
        {
            sw.Stop();
            _logger.LogInformation(
                "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms [CorrelationId={CorrelationId}] [User={UserId}]",
                context.Request.Method, context.Request.Path,
                context.Response.StatusCode, sw.ElapsedMilliseconds,
                context.Items["CorrelationId"], context.User?.FindFirst("sub")?.Value);
        }
    }
}
```

### Integration Points

| Existing Component | Change |
|---|---|
| [`UseKernel()`](file:///d:/Dev/APITemplates/templates/mwa-solution/Kernel/Extensions/WebApplicationBuilderExtensions.cs#L190-L201) | Add `app.UseCorrelationId()` + `app.UseRequestDiagnostics()` before `app.MapControllers()` |
| [`IScopeContext`](file:///d:/Dev/APITemplates/templates/mwa-solution/Kernel/Context/IScopeContext.cs) | Add `string? CorrelationId { get; }` |
| [`Message`](file:///d:/Dev/APITemplates/templates/mwa-solution/Kernel/Messaging/Message.cs) | Add `public string? CorrelationId { get; init; }` |

---

## 7. 📊 Structured Audit Log

**Priority:** 🟡 Medium  
**Location:** `Kernel/AuditLog/`

### Motivation

[`IAuditable`](file:///d:/Dev/APITemplates/templates/mwa-solution/Kernel/Entities/IAuditable.cs) tracks *who* and *when* on individual entities, but there's no centralized, queryable audit trail showing *what changed* (before/after values). Compliance, debugging, and admin investigation require a structured audit log.

### Design Overview

An `AuditEntry` entity capturing entity mutations with property-level diffs, persisted by a `AuditLogSaveChangesInterceptor`. A query endpoint for admins.

### File Inventory

```
Kernel/AuditLog/
├── AuditEntry.cs                  [NEW] — Entity
├── AuditAction.cs                 [NEW] — Enum (Create, Update, Delete)
├── AuditPropertyChange.cs         [NEW] — Before/after pair
├── AuditLogSaveChangesInterceptor.cs [NEW] — Captures changes
├── IAuditLogService.cs            [NEW] — Query interface
├── AuditLogService.cs             [NEW] — Implementation
├── Controllers/
│   └── AuditController.cs         [NEW] — GET api/v1/kernel/audit
└── Features/
    ├── GetAuditEntries.cs         [NEW] — Query request
    └── GetAuditEntriesHandler.cs  [NEW] — Handler

Kernel/Persistence/
├── KernelDbContext.cs             [MODIFY] — Add DbSet<AuditEntry>
```

### Entity Shape

```csharp
public class AuditEntry
{
    public DefaultId Id { get; set; } = DefaultId.NewUlid();
    public DefaultId? UserId { get; set; }
    public required string EntityType { get; init; }
    public required string EntityId { get; init; }
    public required AuditAction Action { get; init; }
    public string? Changes { get; init; }  // JSON array of AuditPropertyChange
    public string? CorrelationId { get; init; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}

public record AuditPropertyChange(string PropertyName, string? OldValue, string? NewValue);

public enum AuditAction { Create, Update, Delete }
```

### Integration Points

| Existing Component | Change |
|---|---|
| [`AddData()`](file:///d:/Dev/APITemplates/templates/mwa-solution/Kernel/Extensions/WebApplicationBuilderExtensions.cs#L140-L181) | Register `AuditLogSaveChangesInterceptor` |
| [`KernelDbContext`](file:///d:/Dev/APITemplates/templates/mwa-solution/Kernel/Persistence/KernelDbContext.cs) | Add `DbSet<AuditEntry> AuditEntries` |

---

## 8. 🗄️ Caching Abstraction

**Priority:** 🟡 Medium  
**Location:** `Kernel/Caching/`

### Motivation

No caching infrastructure exists. Modules needing to cache hot data (user lookups, settings, profile registrations) must each build their own. A kernel-level abstraction enables a future swap to distributed cache without module changes.

### Design Overview

`ICacheService` abstraction with in-memory default. Optional `[Cache]` interceptor attribute for automatic request response caching.

### File Inventory

```
Kernel/Caching/
├── ICacheService.cs               [NEW]
├── InMemoryCacheService.cs        [NEW] — Uses IMemoryCache
├── CacheAttribute.cs              [NEW] — RequestInterceptorAttribute
├── CacheInterceptor.cs            [NEW] — IRequestInterceptor
├── CacheSettings.cs               [NEW] — DynamicSettingsSection
└── Extensions/
    └── CacheExtensions.cs         [NEW] — DI registration
```

### Interface Shapes

```csharp
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default);
    Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default);
}
```

```csharp
// Usage on handler:
[Cache(DurationSeconds = 300, KeyTemplate = "user:{request.Id}")]
public class GetUserHandler : IRequestHandler<GetUser, UserDetailDto> { ... }
```

---

## 9. 🔄 Inbox / Idempotency

**Priority:** 🟡 Medium  
**Location:** `Kernel/Messaging/`

### Motivation

The outbox guarantees at-least-once delivery, but the [`EventMessageProcessor`](file:///d:/Dev/APITemplates/templates/mwa-solution/Kernel/Messaging/EventMessageProcessor.cs) has no idempotency guard. Retried messages can trigger duplicate side effects.

### Design Overview

An `InboxEntry` entity that records processed message IDs. The `EventMessageProcessor` checks the inbox before invoking handlers. Configurable retention/cleanup.

### File Inventory

```
Kernel/Messaging/
├── InboxEntry.cs                  [NEW] — Entity (Id, MessageId, ProcessedAt)
├── IInbox.cs                      [NEW] — Check + record interface
├── Inbox.cs                       [NEW] — EF implementation
├── EventMessageProcessor.cs       [MODIFY] — Add inbox check before processing

Kernel/Persistence/
├── KernelDbContext.cs             [MODIFY] — Add DbSet<InboxEntry>
```

### Interface Shape

```csharp
public interface IInbox
{
    Task<bool> HasBeenProcessedAsync(DefaultId messageId, CancellationToken ct = default);
    Task MarkProcessedAsync(DefaultId messageId, CancellationToken ct = default);
}
```

---

## 10. 🧪 Testing Utilities

**Priority:** 🟡 Medium  
**Location:** `Kernel.Testing/` (new project in solution template)

### Motivation

Zero testing infrastructure. Every consumer project builds its own test harness from scratch. Integration testing the dispatcher/interceptor pipeline requires deep DI setup knowledge.

### Design Overview

A companion class library providing test fakes, base classes, and assertion helpers.

### File Inventory

```
Kernel.Testing/
├── Kernel.Testing.csproj              [NEW]
├── Fakes/
│   ├── FakeEventBus.cs                [NEW] — Captures events for assertions
│   ├── FakeScopeContext.cs            [NEW] — Configurable test identity
│   ├── FakeEmailSender.cs             [NEW] — Captures sent emails
│   └── FakeCacheService.cs            [NEW] — In-memory dict cache
├── Infrastructure/
│   ├── TestWebApplicationFactory.cs   [NEW] — Pre-configured with InMemory DB
│   └── ModuleTestBase.cs              [NEW] — Base class with Dispatcher, EventBus, DbContext
├── Assertions/
│   ├── ResultAssertions.cs            [NEW] — result.ShouldBeSuccess(), result.ShouldBeFailure()
│   └── EventBusAssertions.cs          [NEW] — eventBus.ShouldHavePublished<T>()
```

### Usage Shape

```csharp
public class UserTests : ModuleTestBase<IdentityModule>
{
    [Fact]
    public async Task CreateUser_WithValidData_ShouldPublishEvent()
    {
        var result = await Dispatcher.DispatchAsync(
            new CreateUser("test@example.com", "testuser", "P@ssw0rd!", "User"));

        result.ShouldBeSuccess();
        EventBus.ShouldHavePublished<UserCreatedEvent>(e => e.Email == "test@example.com");
    }
}
```

---

## 11. 🔒 Data Encryption

**Priority:** 🟢 Lower  
**Location:** `Kernel/Encryption/`

### Motivation

Sensitive data (preferences JSON, refresh tokens) stored in plaintext. No encryption infrastructure for column-level encryption at rest.

### Design Overview

`IEncryptionService` abstraction with AES-256-GCM default. `[Encrypted]` EF value converter attribute for transparent property encryption. Key stored in `EncryptionSettings`.

### File Inventory

```
Kernel/Encryption/
├── IEncryptionService.cs          [NEW]
├── AesEncryptionService.cs        [NEW]
├── EncryptedAttribute.cs          [NEW] — EF value converter marker
├── EncryptedValueConverter.cs     [NEW] — EF Core ValueConverter
├── EncryptionSettings.cs          [NEW] — DynamicSettingsSection

Host/
├── appsettings.Encryption.json    [NEW]
```

### Interface Shape

```csharp
public interface IEncryptionService
{
    string Encrypt(string plaintext);
    string Decrypt(string ciphertext);
    byte[] Encrypt(byte[] plaintext);
    byte[] Decrypt(byte[] ciphertext);
}
```

---

## 12. 📜 API Versioning

**Priority:** 🟢 Lower  
**Location:** `Kernel/Versioning/`

### Motivation

Controllers hardcode `api/v1/` routes. No infrastructure for introducing `v2` alongside `v1`, no deprecation headers, no version negotiation. Breaking changes have no supported path.

### Design Overview

Integrate `Asp.Versioning.Http` and `Asp.Versioning.Mvc` packages. URL-segment versioning strategy (`/api/v{n}/...`). Module-level version registry. Deprecation metadata flows into OpenAPI docs.

### File Inventory

```
Kernel/Versioning/
├── VersioningSettings.cs          [NEW] — DynamicSettingsSection (default version, sunset policy)
└── Extensions/
    └── VersioningExtensions.cs    [NEW] — builder.AddApiVersioning() config
```

### Integration Points

| Existing Component | Change |
|---|---|
| `AddKernel()` | Register API versioning services |
| `IModule` | Optionally add `static abstract ApiVersion[] SupportedVersions { get; }` |

---

## 13. 📦 File Storage

**Priority:** 🟢 Lower  
**Location:** `Kernel/Storage/`

### Motivation

Virtually every real API needs file uploads. No storage abstraction exists — modules would independently implement file handling, coupling to specific cloud providers.

### Design Overview

`IFileStorage` abstraction with `UploadAsync`, `DownloadAsync`, `DeleteAsync`, `GetUrlAsync`. Local filesystem default. Swappable to S3/Azure Blob/GCS.

### File Inventory

```
Kernel/Storage/
├── IFileStorage.cs                [NEW]
├── LocalFileStorage.cs            [NEW]
├── FileStorageSettings.cs         [NEW] — DynamicSettingsSection (base path, max size)
└── Extensions/
    └── StorageExtensions.cs       [NEW]

Host/
├── appsettings.Storage.json       [NEW]
```

### Interface Shape

```csharp
public interface IFileStorage
{
    Task<Result<string>> UploadAsync(string path, Stream content, string? contentType = null, CancellationToken ct = default);
    Task<Result<Stream>> DownloadAsync(string path, CancellationToken ct = default);
    Task<Result> DeleteAsync(string path, CancellationToken ct = default);
    Task<Result<string>> GetUrlAsync(string path, TimeSpan? expiry = null, CancellationToken ct = default);
    Task<Result<bool>> ExistsAsync(string path, CancellationToken ct = default);
}
```

---

## 14. 🔔 Real-Time Notifications

**Priority:** 🟢 Lower  
**Location:** `Kernel/Notifications/`

### Motivation

The event bus is server-side only. No way to push events to connected clients in real time. Modern APIs need WebSocket/SSE for live updates.

### Design Overview

`INotificationService` abstraction with SignalR default. Integrated with `IEventBus` — handlers can push notifications. Per-user and per-group targeting.

### File Inventory

```
Kernel/Notifications/
├── INotificationService.cs        [NEW]
├── SignalRNotificationService.cs  [NEW]
├── NotificationHub.cs             [NEW] — SignalR Hub
├── NotificationSettings.cs        [NEW] — DynamicSettingsSection (enabled, path)
└── Extensions/
    └── NotificationExtensions.cs  [NEW]
```

### Interface Shape

```csharp
public interface INotificationService
{
    Task SendToUserAsync(DefaultId userId, string channel, object payload, CancellationToken ct = default);
    Task SendToGroupAsync(string group, string channel, object payload, CancellationToken ct = default);
    Task SendToAllAsync(string channel, object payload, CancellationToken ct = default);
}
```

---

## 15. 🔑 API Key Authentication

**Priority:** 🟢 Lower  
**Location:** `mwa-module-identity`

### Motivation

The Identity module only supports JWT (interactive user login). Server-to-server integrations, CLI tools, and webhooks need API key authentication.

### Design Overview

`ApiKey` entity with hashed key storage. Key generation/revocation endpoints. `ApiKeyAuthenticationHandler` reads `X-API-Key` header. Keys are scoped to users and inherit their roles/claims.

### File Inventory

```
Modules/Identity/
├── Entities/
│   └── ApiKey.cs                  [NEW] — Entity (Id, KeyHash, Name, UserId, ExpiresAt, RevokedAt)
├── Services/
│   └── IApiKeyService.cs          [NEW] — Generate, Validate, Revoke
│   └── ApiKeyService.cs           [NEW]
├── Authentication/
│   └── ApiKeyAuthenticationHandler.cs [NEW]
├── Controllers/
│   └── ApiKeysController.cs       [NEW] — CRUD endpoints
├── Features/ApiKeys/
│   ├── CreateApiKey.cs            [NEW]
│   ├── RevokeApiKey.cs            [NEW]
│   └── GetApiKeys.cs             [NEW]

Modules/Identity/Persistence/
├── IdentityModuleDbContext.cs     [MODIFY] — Add DbSet<ApiKey>
```

### Entity Shape

```csharp
public class ApiKey : IAuditable
{
    public DefaultId Id { get; set; } = DefaultId.NewUlid();
    public required string Name { get; init; }          // human-readable label
    public required string KeyHash { get; init; }       // SHA-256 of the key
    public required string KeyPrefix { get; init; }     // first 8 chars for identification
    public required DefaultId UserId { get; init; }     // owner
    public DateTime? ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }

    // IAuditable
    public DateTime CreatedAt { get; set; }
    public DefaultId? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DefaultId? UpdatedBy { get; set; }
}
```

---

## Summary Checklist

- [ ] **#1 — Authorization Policies** (`IAuthorizationPolicy` + dispatcher interceptor + endpoint/controller filters)
- [ ] **#2 — Rate Limiting** (ASP.NET middleware + `RateLimitingSettings` + per-endpoint config)
- [ ] **#3 — Email Channel** (`IEmailSender` + SMTP impl + `EmailMessageProcessor`)
- [ ] **#4 — Validation Interceptor** (`IValidator<T>` + `[Validate]` attribute + auto-discovery)
- [ ] **#5 — Pagination & Filtering** (`PaginatedQuery` + `IQueryable` extensions + `PagedResult` improvements)
- [ ] **#6 — Observability** (correlation ID + request diagnostics middleware)
- [ ] **#7 — Structured Audit Log** (`AuditEntry` + SaveChanges interceptor + query endpoint)
- [ ] **#8 — Caching** (`ICacheService` + in-memory default + `[Cache]` interceptor)
- [ ] **#9 — Inbox / Idempotency** (`InboxEntry` + inbox check in `EventMessageProcessor`)
- [ ] **#10 — Testing Utilities** (fakes + test base classes + assertion helpers)
- [ ] **#11 — Data Encryption** (`IEncryptionService` + `[Encrypted]` EF converter)
- [ ] **#12 — API Versioning** (`Asp.Versioning` integration + module-level versions)
- [ ] **#13 — File Storage** (`IFileStorage` + local filesystem default)
- [ ] **#14 — Real-Time Notifications** (`INotificationService` + SignalR hub)
- [ ] **#15 — API Key Auth** (`ApiKey` entity + auth handler + endpoints)
