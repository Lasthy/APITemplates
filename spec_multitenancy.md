# Multi-Tenancy Module — Specification

**Template:** `mwa-module-multitenancy` · **Short Name:** `modular-webapi-module-multitenancy` · **Type:** Project

This module provides multi-tenant isolation for MWA applications: tenant lifecycle management, member & invitation management, configurable tenant resolution (header, subdomain, claim, path), automatic EF Core global query filters for data isolation, and Identity profile integration.

---

## Module Anatomy

```
Modules/
├── MultiTenancy/                                     # Module implementation
│   ├── Configuration/
│   │   └── TenantSettings.cs                        # DynamicSettingsSection: resolution, limits, invitations
│   ├── Controllers/
│   │   ├── TenantsController.cs                     # Tenant CRUD, suspend/reactivate
│   │   ├── MembersController.cs                     # Member list, add, remove, update role
│   │   └── InvitationsController.cs                 # Invite, accept, revoke, list invitations
│   ├── Entities/
│   │   ├── Tenant.cs                                # Tenant entity (name, slug, status, plan, metadata)
│   │   ├── TenantMember.cs                          # User ↔ Tenant relationship (role, joined date)
│   │   └── TenantInvitation.cs                      # Pending invitation (email, role, token, expiry)
│   ├── Features/
│   │   ├── Tenants/                                 # CreateTenant, GetTenants, GetTenant, UpdateTenant, SuspendTenant, ReactivateTenant, DeleteTenant
│   │   ├── Members/                                 # GetMembers, AddMember, RemoveMember, UpdateMemberRole, GetCurrentMembership
│   │   ├── Invitations/                             # CreateInvitation, AcceptInvitation, RevokeInvitation, GetInvitations
│   │   ├── GetTenantSummaryHandler.cs               # Cross-module handler for GetTenantSummary
│   │   └── ValidateTenantAccessHandler.cs           # Cross-module handler for ValidateTenantAccess
│   ├── Middleware/
│   │   └── TenantResolutionMiddleware.cs            # Resolves current tenant per request
│   ├── Persistence/
│   │   └── TenantModuleDbContext.cs                 # DbContext with "tenancy" schema
│   ├── Resolution/
│   │   ├── ITenantResolver.cs                       # Abstraction for tenant resolution strategies
│   │   ├── HeaderTenantResolver.cs                  # Resolves from X-Tenant-Id header
│   │   ├── SubdomainTenantResolver.cs               # Resolves from subdomain (acme.api.com)
│   │   ├── ClaimTenantResolver.cs                   # Resolves from authenticated user's TenantId claim
│   │   ├── RouteSegmentTenantResolver.cs            # Resolves from URL path segment (/api/v1/{tenantSlug}/...)
│   │   └── CompositeTenantResolver.cs               # Chains multiple resolvers with priority order
│   ├── Services/
│   │   ├── ITenantService.cs                        # Tenant lifecycle operations
│   │   ├── TenantService.cs                         # Implementation
│   │   ├── ITenantContext.cs                        # Scoped current-tenant accessor
│   │   └── TenantContext.cs                         # Implementation (set by middleware)
│   ├── Workers/
│   │   └── InvitationCleanupWorker.cs               # Periodic: purge expired invitations
│   ├── MultiTenancyModule.cs                        # IModule entry point
│   ├── appsettings.MultiTenancy.json                # Module settings
│   └── appsettings.MultiTenancy.Development.json    # Dev overlay
│
└── MultiTenancy.Contracts/                           # Companion contracts
    ├── Constants/
    │   ├── TenantRoles.cs                           # Owner, Admin, Member, ReadOnly
    │   ├── TenantPolicies.cs                        # TenantOwner, TenantAdmin, TenantMember
    │   ├── TenantClaimTypes.cs                      # TenantId, TenantSlug, TenantRole
    │   └── TenantStatus.cs                          # Active, Suspended, Deleted
    ├── Dtos/
    │   ├── TenantDto.cs                             # Summary DTO
    │   ├── TenantDetailDto.cs                       # Full DTO with member count, plan, metadata
    │   ├── TenantMemberDto.cs                       # Member summary
    │   └── TenantInvitationDto.cs                   # Invitation summary
    ├── Events/
    │   ├── TenantCreatedEvent.cs                    # (TenantId, Name, Slug, OwnerUserId)
    │   ├── TenantSuspendedEvent.cs                  # (TenantId, Reason, SuspendedBy)
    │   ├── TenantReactivatedEvent.cs                # (TenantId, ReactivatedBy)
    │   ├── TenantDeletedEvent.cs                    # (TenantId, DeletedBy)
    │   ├── MemberAddedEvent.cs                      # (TenantId, UserId, Role)
    │   ├── MemberRemovedEvent.cs                    # (TenantId, UserId, RemovedBy)
    │   ├── MemberRoleChangedEvent.cs                # (TenantId, UserId, OldRole, NewRole)
    │   ├── InvitationCreatedEvent.cs                # (TenantId, Email, Role, InvitedBy)
    │   └── InvitationAcceptedEvent.cs               # (TenantId, UserId, InvitationId)
    ├── Requests/
    │   ├── GetTenantSummary.cs                      # IRequest<TenantDto> — query by tenant ID
    │   └── ValidateTenantAccess.cs                  # IRequest<bool> — check user has role in tenant
    └── MultiTenancy.Contracts.csproj
```

---

## 1. Entities

### `Tenant`

```csharp
public class Tenant : IAuditable, ISoftDeletable
{
    public DefaultId Id { get; set; }

    /// <summary>
    /// Human-readable display name (e.g., "Acme Corporation").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// URL-safe unique identifier derived from name (e.g., "acme-corporation").
    /// Used in subdomain and route-segment resolution strategies.
    /// </summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// Current lifecycle status: Active, Suspended, or Deleted.
    /// </summary>
    public TenantStatus Status { get; set; } = TenantStatus.Active;

    /// <summary>
    /// Optional plan/tier identifier for billing or feature gating (e.g., "free", "pro", "enterprise").
    /// </summary>
    public string? Plan { get; set; }

    /// <summary>
    /// Freeform JSON metadata blob for tenant-specific configuration.
    /// </summary>
    public string? MetadataJson { get; set; }

    /// <summary>
    /// The user who created/owns this tenant. Set on creation, transferable.
    /// </summary>
    public DefaultId OwnerUserId { get; set; }

    /// <summary>
    /// Optional reason for suspension.
    /// </summary>
    public string? SuspensionReason { get; set; }
    public DateTime? SuspendedAt { get; set; }
    public DefaultId? SuspendedBy { get; set; }

    // ── IAuditable ──
    public DateTime CreatedAt { get; set; }
    public DefaultId? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DefaultId? UpdatedBy { get; set; }

    // ── ISoftDeletable ──
    public DateTime? DeletedAt { get; set; }
    public DefaultId? DeletedBy { get; set; }
}
```

### `TenantMember`

```csharp
public class TenantMember : IAuditable
{
    public DefaultId Id { get; set; }
    public DefaultId TenantId { get; set; }
    public DefaultId UserId { get; set; }

    /// <summary>
    /// Role within the tenant: Owner, Admin, Member, ReadOnly.
    /// </summary>
    public string Role { get; set; } = TenantRoles.Member;

    public DateTime JoinedAt { get; set; }

    // Navigation
    public Tenant Tenant { get; set; } = null!;

    // ── IAuditable ──
    public DateTime CreatedAt { get; set; }
    public DefaultId? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DefaultId? UpdatedBy { get; set; }
}
```

### `TenantInvitation`

```csharp
public class TenantInvitation : IAuditable
{
    public DefaultId Id { get; set; }
    public DefaultId TenantId { get; set; }

    /// <summary>
    /// Email address of the invited user.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Role to assign upon acceptance.
    /// </summary>
    public string Role { get; set; } = TenantRoles.Member;

    /// <summary>
    /// Unique invitation token (URL-safe, cryptographically random).
    /// </summary>
    public string Token { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }
    public bool IsAccepted { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DefaultId? AcceptedByUserId { get; set; }
    public bool IsRevoked { get; set; }

    // Navigation
    public Tenant Tenant { get; set; } = null!;

    // ── IAuditable ──
    public DateTime CreatedAt { get; set; }
    public DefaultId? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DefaultId? UpdatedBy { get; set; }
}
```

---

## 2. Tenant Resolution

Tenant resolution determines the "current tenant" for each request. The module ships multiple strategies, selectable via `TenantSettings.ResolutionStrategy`.

### Resolution Strategies

| Strategy | Key | How it works |
|---|---|---|
| **Header** | `header` | Reads `X-Tenant-Id` or `X-Tenant-Slug` request header. Default strategy. |
| **Subdomain** | `subdomain` | Extracts the first subdomain segment (e.g., `acme` from `acme.api.example.com`). |
| **Claim** | `claim` | Reads the `TenantId` claim from the authenticated user's `ClaimsPrincipal`. |
| **Route** | `route` | Reads `{tenantSlug}` from the URL path (requires route template cooperation). |
| **Composite** | `composite` | Chains multiple resolvers in priority order (default: header → claim → subdomain). |

### `ITenantResolver`

```csharp
public interface ITenantResolver
{
    /// <summary>
    /// Attempts to resolve a tenant ID from the current HTTP context.
    /// Returns null if the strategy cannot resolve a tenant.
    /// </summary>
    Task<DefaultId?> ResolveAsync(HttpContext context, CancellationToken ct = default);
}
```

### `ITenantContext` (Scoped)

```csharp
public interface ITenantContext
{
    /// <summary>
    /// The resolved tenant for the current request. Null if no tenant was resolved
    /// (e.g., anonymous tenant-agnostic endpoints).
    /// </summary>
    DefaultId? TenantId { get; }

    /// <summary>
    /// Whether a tenant was resolved for the current request.
    /// </summary>
    bool HasTenant { get; }

    /// <summary>
    /// The resolved tenant entity. Lazy-loaded on first access.
    /// </summary>
    Task<Tenant?> GetTenantAsync(CancellationToken ct = default);
}
```

### `TenantResolutionMiddleware`

Injected into the ASP.NET pipeline in `MultiTenancyModule.MapEndpoints()` via `app.UseMiddleware<TenantResolutionMiddleware>()`. Runs after authentication (so claims are available) but before authorization and endpoint execution.

**Behavior:**
1. Invokes `ITenantResolver.ResolveAsync()` to obtain a `TenantId`.
2. If resolved, validates the tenant exists and is `Active`. Returns `403` for suspended tenants, `404` for unknown tenants.
3. Sets `ITenantContext.TenantId` for the remainder of the request scope.
4. If `TenantSettings.RequireTenant` is `true` and no tenant is resolved, returns `400` (unless the endpoint is decorated with `[TenantOptional]`).
5. If `TenantSettings.RequireTenant` is `false`, unresolved requests proceed without a tenant (multi-tenant-optional mode).

### `[TenantOptional]` Attribute

Marker attribute for endpoints that do not require tenant resolution even when `RequireTenant` is `true` (e.g., tenant creation, tenant listing for System users).

---

## 3. Data Isolation — `ITenantScoped`

The module provides an `ITenantScoped` marker interface that any entity in any module can implement to opt into automatic tenant-scoped filtering:

```csharp
// In MultiTenancy.Contracts
public interface ITenantScoped
{
    DefaultId TenantId { get; set; }
}
```

### How other modules use it

```csharp
// In a Product module entity
public class Product : ITenantScoped, IAuditable
{
    public DefaultId Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DefaultId TenantId { get; set; }
    // ...
}
```

### Global Query Filter Integration

The `MultiTenancyModule.Register()` method registers a **SaveChanges interceptor** and a **model customization convention** that:

1. **On `OnModelCreating`:** Scans all entity types implementing `ITenantScoped` and applies `.HasQueryFilter(e => e.TenantId == currentTenantId)` using the scoped `ITenantContext`.
2. **On `SaveChangesAsync`:** Automatically stamps `TenantId` on new entities implementing `ITenantScoped` from the current `ITenantContext`. Throws if `ITenantContext.HasTenant` is `false` and the entity has no `TenantId` set.

> **Important:** This requires each module's `DbContext` to call `builder.ApplyTenantFilters(tenantContext)` in its `OnModelCreating`. The module provides an extension method for this.

```csharp
// Extension method provided by MultiTenancy
public static class TenantModelBuilderExtensions
{
    public static void ApplyTenantFilters(this ModelBuilder builder, ITenantContext tenantContext) { ... }
}
```

---

## 4. Persistence

### `TenantModuleDbContext`

```csharp
public class TenantModuleDbContext : DbContext, IDbContextSchema
{
    public string Schema => "tenancy";

    public DbSet<Tenant> Tenants { get; set; } = null!;
    public DbSet<TenantMember> Members { get; set; } = null!;
    public DbSet<TenantInvitation> Invitations { get; set; } = null!;

    // Standard: ConfigureConventions with Ulid converters,
    //           OnModelCreating with ApplySchema, entity config, indexes.
}
```

**Key indexes:**
- `Tenant.Slug` — unique
- `Tenant.OwnerUserId`
- `Tenant.Status`
- `TenantMember.(TenantId, UserId)` — unique composite
- `TenantMember.UserId`
- `TenantInvitation.(TenantId, Email)` — unique composite (for pending invitations)
- `TenantInvitation.Token` — unique

---

## 5. Services

### `ITenantService`

```csharp
public interface ITenantService
{
    Task<Result<Tenant>> CreateAsync(string name, DefaultId ownerUserId, string? plan = null, CancellationToken ct = default);
    Task<Result<Tenant>> GetByIdAsync(DefaultId tenantId, CancellationToken ct = default);
    Task<Result<Tenant>> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<Result> UpdateAsync(DefaultId tenantId, string? name, string? plan, string? metadataJson, CancellationToken ct = default);
    Task<Result> SuspendAsync(DefaultId tenantId, string? reason, CancellationToken ct = default);
    Task<Result> ReactivateAsync(DefaultId tenantId, CancellationToken ct = default);
    Task<Result> DeleteAsync(DefaultId tenantId, CancellationToken ct = default);

    // Members
    Task<Result<TenantMember>> AddMemberAsync(DefaultId tenantId, DefaultId userId, string role, CancellationToken ct = default);
    Task<Result> RemoveMemberAsync(DefaultId tenantId, DefaultId userId, CancellationToken ct = default);
    Task<Result> UpdateMemberRoleAsync(DefaultId tenantId, DefaultId userId, string newRole, CancellationToken ct = default);
    Task<Result<IEnumerable<TenantMember>>> GetMembersAsync(DefaultId tenantId, CancellationToken ct = default);

    // Invitations
    Task<Result<TenantInvitation>> InviteAsync(DefaultId tenantId, string email, string role, CancellationToken ct = default);
    Task<Result> AcceptInvitationAsync(string token, DefaultId userId, CancellationToken ct = default);
    Task<Result> RevokeInvitationAsync(DefaultId invitationId, CancellationToken ct = default);

    // Slug generation
    string GenerateSlug(string name);
}
```

---

## 6. Configuration

### `TenantSettings` (`DynamicSettingsSection`)

```csharp
public class TenantSettings : DynamicSettingsSection
{
    public override string SectionName => "MultiTenancy";

    // ... constructor with appsettings.MultiTenancy.json ...
}
```

### `appsettings.MultiTenancy.json`

```json
{
  "ResolutionStrategy": "header",
  "HeaderName": "X-Tenant-Id",
  "RequireTenant": false,
  "MaxTenantsPerUser": 5,
  "MaxMembersPerTenant": 100,
  "InvitationExpiryHours": 72,
  "InvitationCleanupIntervalMinutes": 60,
  "DefaultPlan": "free",
  "AllowSlugChange": false,
  "CompositeResolverOrder": ["header", "claim", "subdomain"]
}
```

| Key | Type | Default | Purpose |
|---|---|---|---|
| `ResolutionStrategy` | `string` | `"header"` | Active resolution strategy: `header`, `subdomain`, `claim`, `route`, `composite` |
| `HeaderName` | `string` | `"X-Tenant-Id"` | Header name for the header resolver |
| `RequireTenant` | `bool` | `false` | If `true`, all requests must resolve a tenant (unless `[TenantOptional]`) |
| `MaxTenantsPerUser` | `int` | `5` | Max tenants a single user can own |
| `MaxMembersPerTenant` | `int` | `100` | Max members per tenant |
| `InvitationExpiryHours` | `int` | `72` | Hours before a pending invitation expires |
| `InvitationCleanupIntervalMinutes` | `int` | `60` | Cleanup worker interval |
| `DefaultPlan` | `string` | `"free"` | Plan assigned to new tenants by default |
| `AllowSlugChange` | `bool` | `false` | Whether tenants can change their slug after creation |
| `CompositeResolverOrder` | `string[]` | `["header","claim","subdomain"]` | Priority order for composite resolver |

---

## 7. API Endpoints

### Tenants (`/api/v1/multitenancy/tenants`)

| Verb | Path | Auth | Description |
|---|---|---|---|
| `POST` | `/` | Authenticated | Create a new tenant (caller becomes Owner) |
| `GET` | `/` | System | List all tenants (paged, filterable by status) |
| `GET` | `/mine` | Authenticated | List tenants the current user is a member of |
| `GET` | `/{id}` | TenantMember / System | Get tenant details |
| `PUT` | `/{id}` | TenantOwner / TenantAdmin / System | Update tenant name, plan, metadata |
| `POST` | `/{id}/suspend` | System | Suspend a tenant |
| `POST` | `/{id}/reactivate` | System | Reactivate a suspended tenant |
| `DELETE` | `/{id}` | TenantOwner / System | Soft-delete a tenant |

### Members (`/api/v1/multitenancy/tenants/{tenantId}/members`)

| Verb | Path | Auth | Description |
|---|---|---|---|
| `GET` | `/` | TenantMember / System | List members of a tenant (paged) |
| `POST` | `/` | TenantAdmin / TenantOwner / System | Add a user as a member |
| `PUT` | `/{userId}` | TenantOwner / System | Update a member's role |
| `DELETE` | `/{userId}` | TenantAdmin / TenantOwner / System / Self | Remove a member |
| `GET` | `/me` | TenantMember | Get current user's membership in this tenant |

### Invitations (`/api/v1/multitenancy/tenants/{tenantId}/invitations`)

| Verb | Path | Auth | Description |
|---|---|---|---|
| `POST` | `/` | TenantAdmin / TenantOwner / System | Send an invitation |
| `GET` | `/` | TenantAdmin / TenantOwner / System | List pending invitations |
| `DELETE` | `/{id}` | TenantAdmin / TenantOwner / System | Revoke an invitation |
| `POST` | `/accept` | Authenticated (tenant-optional) | Accept an invitation by token |

---

## 8. Cross-Module Contracts

### DTOs

```csharp
public record TenantDto(DefaultId Id, string Name, string Slug, string Status, string? Plan, DateTime CreatedAt);
public record TenantDetailDto(DefaultId Id, string Name, string Slug, string Status, string? Plan, string? MetadataJson,
    DefaultId OwnerUserId, int MemberCount, DateTime CreatedAt, DateTime? SuspendedAt);
public record TenantMemberDto(DefaultId UserId, string Role, DateTime JoinedAt);
public record TenantInvitationDto(DefaultId Id, string Email, string Role, DateTime ExpiresAt, bool IsAccepted);
```

### Events

```csharp
public record TenantCreatedEvent(DefaultId TenantId, string Name, string Slug, DefaultId OwnerUserId) : Event;
public record TenantSuspendedEvent(DefaultId TenantId, string? Reason, DefaultId SuspendedBy) : Event;
public record TenantReactivatedEvent(DefaultId TenantId, DefaultId ReactivatedBy) : Event;
public record TenantDeletedEvent(DefaultId TenantId, DefaultId DeletedBy) : Event;
public record MemberAddedEvent(DefaultId TenantId, DefaultId UserId, string Role) : Event;
public record MemberRemovedEvent(DefaultId TenantId, DefaultId UserId, DefaultId RemovedBy) : Event;
public record MemberRoleChangedEvent(DefaultId TenantId, DefaultId UserId, string OldRole, string NewRole) : Event;
public record InvitationCreatedEvent(DefaultId TenantId, string Email, string Role, DefaultId InvitedBy) : Event;
public record InvitationAcceptedEvent(DefaultId TenantId, DefaultId UserId, DefaultId InvitationId) : Event;
```

### Cross-Module Requests

```csharp
public record GetTenantSummary(DefaultId TenantId) : IRequest<TenantDto>;
public record ValidateTenantAccess(DefaultId TenantId, DefaultId UserId, string? RequiredRole = null) : IRequest<bool>;
```

### Constants

```csharp
public static class TenantRoles
{
    public const string Owner = "Owner";
    public const string Admin = "Admin";
    public const string Member = "Member";
    public const string ReadOnly = "ReadOnly";
}

public static class TenantClaimTypes
{
    public const string TenantId = "tenant_id";
    public const string TenantSlug = "tenant_slug";
    public const string TenantRole = "tenant_role";
}

public enum TenantStatus { Active, Suspended, Deleted }
```

---

## 9. Identity Integration

When the Identity module is also active, MultiTenancy can register custom profiles and inject tenant claims.

### Custom Profiles (Optional, Code-Defined)

```csharp
public class TenantOwnerProfile : IProfile
{
    public static string Name => "TenantOwner";
    public static string Description => "Owner of a tenant organization.";
    public static bool IsPublic => false;
    public static IReadOnlyList<IProfileCreationRequirement> CreationRequirements =>
        [ProfileRequirements.RequireAuthenticated()];
    public static IReadOnlyList<string> Roles => [];
    public static IReadOnlyDictionary<string, string> Claims => new Dictionary<string, string>();
    public static Type? PreferencesType => null;
    public static bool CanManageApiKeys => true;

    public static async Task OnUserCreatedAsync(ProfileCreationContext context, CancellationToken ct)
    {
        // Create a default tenant for the new user
        var tenantService = context.Services.GetRequiredService<ITenantService>();
        var tenantName = context.GetAdditionalData<string>("OrganizationName") ?? context.User.UserName ?? "My Organization";
        var result = await tenantService.CreateAsync(tenantName, context.User.Id, ct: ct);
        if (result.IsSuccess)
        {
            await context.AddClaimAsync(TenantClaimTypes.TenantId, result.Value.Id.ToString());
            await context.AddClaimAsync(TenantClaimTypes.TenantSlug, result.Value.Slug);
        }
    }
}
```

### Event Handlers

The module subscribes to `UserCreatedEvent` to handle tenant auto-provisioning scenarios, and to `UserBannedEvent` to cascade member deactivation if configured.

---

## 10. Workers

### `InvitationCleanupWorker`

Periodic `BaseWorker` that purges expired, unaccepted invitations. Runs at `TenantSettings.InvitationCleanupIntervalMinutes` interval.

---

## 11. Host Integration

```csharp
// Program.cs — after app.UseAuthentication() and app.UseAuthorization()
// MultiTenancy middleware is auto-registered by MapEndpoints().
// No manual middleware registration needed.
```

`appsettings.Instance.json`:
```json
{
  "ActiveModules": {
    "MultiTenancy": true
  }
}
```

---

## 12. Rules & Best Practices

1. **Tenant resolution runs per-request.** The `ITenantContext` is scoped — inject it, never cache it.
2. **Use `ITenantScoped` on entities, not manual `WHERE` clauses.** The global query filter handles all reads; the interceptor handles all writes.
3. **System users bypass tenant scoping.** When `IScopeContext.IsSystem` is `true`, tenant query filters are disabled (System sees all data).
4. **Never reference MultiTenancy implementation from other modules.** Reference `MultiTenancy.Contracts` only.
5. **Tenant roles are distinct from Identity roles.** `TenantRoles.Admin` is not the same as `Roles.System`. A user can be `TenantOwner` in one tenant and `ReadOnly` in another.
6. **Slug is immutable by default.** Slug changes break subdomain and route resolution. Only enable `AllowSlugChange` if your resolution strategy doesn't depend on slugs.
7. **Cascading suspension:** When a tenant is suspended, all API requests resolving to that tenant receive `403 Forbidden`. Members are not deleted — they simply cannot operate until the tenant is reactivated.
8. **Invitation tokens are single-use.** Once accepted or revoked, the token is permanently invalidated.
