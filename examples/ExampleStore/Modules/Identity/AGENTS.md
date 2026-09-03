# Identity — Module Agent Guide

This project is a self-contained **Modular Web API (MWA)** Identity module. It provides user management, profile-based registration, JWT authentication with refresh token rotation, user preferences, and admin operations — all built on ASP.NET Identity with `DefaultId` (Ulid) as the key type.

---

## Module Anatomy

```
Modules/
├── Identity/                                     # Module implementation
│   ├── Configuration/
│   │   └── IdentitySettings.cs                   # DynamicSettingsSection: JWT, cookie, seeding config
│   ├── Controllers/
│   │   ├── AuthController.cs                     # Login, refresh, logout, me
│   │   ├── ApiKeysController.cs                  # Create, list, get, revoke API keys
│   │   ├── UsersController.cs                    # Register, CRUD, ban/unban, preferences, dummy
│   │   └── ProfilesController.cs                 # List/detail profiles
│   ├── Entities/
│   │   ├── ApplicationUser.cs                    # IdentityUser<DefaultId> + profile, preferences, ban, dummy
│   │   ├── ApplicationRole.cs                    # IdentityRole<DefaultId>
│   │   ├── ApiKey.cs                             # ApiKey entity with SHA-256 hash storage
│   │   └── RefreshToken.cs                       # Refresh token entity with rotation
│   ├── Features/
│   │   ├── Auth/                                 # Login, RefreshToken, Logout, GetCurrentUser, ChangePassword, ForgotPassword, ResetPassword
│   │   ├── ApiKeys/                              # CreateApiKey, RevokeApiKey, GetApiKeys, GetApiKeyById
│   │   ├── Users/                                # CreateUser, GetUsers, GetUser, UpdateUser, Ban/Unban, Dummy, Preferences
│   │   ├── Profiles/                             # GetProfiles, GetProfile
│   │   ├── GetUserSummaryHandler.cs              # Cross-module handler for GetUserSummary
│   │   └── ValidateUserAccessHandler.cs          # Cross-module handler for ValidateUserAccess
│   ├── Authentication/
│   │   └── ApiKeyAuthenticationHandler.cs        # Custom auth handler for X-API-Key scheme
│   ├── Persistence/
│   │   └── IdentityModuleDbContext.cs            # IdentityDbContext with Ulid converters & ApiKeys DbSet
│   ├── Profiles/
│   │   ├── IProfile.cs                           # Profile contract (static abstract interface)
│   │   ├── IProfileCreationRequirement.cs        # Creation requirement interface
│   │   ├── ProfileCreationRequirementContext.cs  # Context for creation requirement evaluation
│   │   ├── ProfileRequirements.cs                # Built-in requirements + fluent combinators
│   │   ├── IProfileRequirementEvaluator.cs       # Service evaluating creation requirements
│   │   ├── ProfileRegistry.cs                    # Singleton registry + ProfileDescriptor
│   │   ├── ProfileCreationContext.cs             # Context for profile creation hooks
│   │   └── DefaultProfiles/
│   │       ├── UserProfile.cs                    # Public user profile
│   │       └── SystemProfile.cs                  # Non-public system profile
│   ├── Services/
│   │   ├── ITokenService.cs / TokenService.cs    # JWT + refresh token generation
│   │   ├── IApiKeyService.cs / ApiKeyService.cs  # API key generation, validation, revocation
│   │   └── IUserService.cs / UserService.cs      # High-level user operations
│   ├── Workers/
│   │   └── RoleSeedWorker.cs                     # One-shot: seeds roles + optional admin user
│   ├── IdentityModule.cs                         # IModule entry point (registers PolicyScheme routing)
│   ├── appsettings.Identity.json                 # Module settings (JWT config, seeding)
│   └── appsettings.Identity.Development.json     # Dev overlay
│
└── Identity.Contracts/                           # Companion contracts
    ├── Constants/                                # IdentityRoles, IdentityPolicies, IdentityProfiles, IdentityClaimTypes
    ├── Dtos/                                     # UserDto, UserDetailDto, AuthTokenDto, ApiKeyDto, ApiKeyCreatedDto, ProfileDto, ProfileDetailDto
    ├── Events/                                   # UserCreatedEvent, UserBannedEvent, UserUnbannedEvent, ApiKeyCreatedEvent, ApiKeyRevokedEvent, PasswordChangedEvent, PasswordResetRequestedEvent, PasswordResetCompletedEvent
    ├── Requests/                                 # GetUserSummary, ValidateUserAccess
    └── Identity.Contracts.csproj
```

---

## 1. Profile System

Profiles are **code-defined** (not DB-stored), discovered at startup via assembly scanning, and registered in `ProfileRegistry`. Adding new profiles is a code change — by design.

### Creating a Custom Profile

```csharp
public class TenantProfile : IProfile
{
    public static string Name => "Tenant";
    public static string Description => "Tenant administrator with org-scoped access.";
    public static bool IsPublic => false;
    public static IReadOnlyList<IProfileCreationRequirement> CreationRequirements => [
        ProfileRequirements.RequireRole("Admin")
    ];
    public static IReadOnlyList<string> Roles => ["User", "TenantAdmin"];
    public static IReadOnlyDictionary<string, string> Claims => new Dictionary<string, string>();
    public static Type? PreferencesType => typeof(TenantPreferences);

    public static async Task OnUserCreatedAsync(ProfileCreationContext context, CancellationToken ct)
    {
        // Create a companion Tenant entity using additional data passed during registration
        var orgName = context.GetAdditionalData<string>("OrganizationName") ?? context.User.UserName;
        var tenantService = context.Services.GetRequiredService<ITenantService>();
        var tenant = await tenantService.CreateAsync(orgName, ct);
        await context.AddClaimAsync("TenantId", tenant.Id.ToString());
    }
}
```

Key points:
- **`IsPublic`**: Indicates whether unauthenticated users can self-register with this profile. If `CreationRequirements` is defined, this property reflects whether any requirement allows public access.
- **`CreationRequirements`**: Requirements that must be satisfied by the caller to create a user with this profile. Evaluated automatically during registration via `IProfileRequirementEvaluator`. Defaults to `[ProfileRequirements.Public]` when `IsPublic` is `true`, or `[ProfileRequirements.RequireSystem()]` when `IsPublic` is `false`. Built-in requirements include:
  - `ProfileRequirements.Public` / `AllowAnonymous()`: Public anonymous registration.
  - `ProfileRequirements.RequireAuthenticated()`: Requires caller to be authenticated.
  - `ProfileRequirements.RequireRole("Admin")` / `RequireAnyRole(...)`: Requires caller to have specific role(s) (or System).
  - `ProfileRequirements.RequireAllRoles(...)`: Requires caller to have all specified roles (or System).
  - `ProfileRequirements.RequireClaim("dept", "IT")`: Requires caller to possess specific claim (and optional value).
  - `ProfileRequirements.RequireCallerProfile("TenantAdmin")`: Requires caller's profile claim to match.
  - `ProfileRequirements.RequireSystem()`: Requires System super-user role.
  - `ProfileRequirements.RequirePolicy("PolicyName")` / `RequirePolicy<TPolicy>()`: Evaluates Kernel `IAuthorizationPolicy`.
  - `ProfileRequirements.Custom(predicate / asyncFunc)`: Custom lambda / delegate logic.
  - `ProfileRequirements.Any(...)` and `ProfileRequirements.All(...)`: Logical OR / AND combinators.
- **`Roles`**: Automatically assigned on user creation. Roles are seeded at startup by `RoleSeedWorker`.
- **`Claims`**: Static claims assigned on user creation.
- **`PreferencesType`**: If set, the user preferences JSON is validated against this type on update.
- **`CanManageApiKeys`**: If `true`, users with this profile are permitted to create and manage their own API keys. Defaults to `false` (e.g. public `BuyerProfile` is `false`, `AdminProfile` and `SystemProfile` are `true`).
- **`OnUserCreatedAsync`**: Hook for side effects (creating companion entities, adding dynamic claims). Receives `ProfileCreationContext` which provides `User`, `UserManager`, scoped `Services`, and `AdditionalData` (with helper methods `GetAdditionalData<T>()`, `TryGetAdditionalData<T>()`, and transient `Items` dictionary).
- **`AdditionalData`**: Custom/additional data passed via `RegisterRequest`, `CreateUser`, and `CreateUserAsync`. Accessible in `ProfileCreationContext` and forwarded in `UserCreatedEvent`. Useful for companion entity payloads (Organization name, Address, Tenant ID, etc.).

### Profile Discovery

All `IProfile` implementations in the Identity module assembly are auto-discovered during `IdentityModule.Register()`. To add profiles from **other modules**, you would need to extend the discovery mechanism (e.g., provide a `ProfileRegistry` extension method).

---

## 2. Authentication Flow

### Dual Scheme (JWT + API Key)

The Identity module configures a default `PolicyScheme` (`IdentityAuth`) that automatically routes incoming requests:
1. If the request contains an `X-API-Key` or `ApiKey` header, it routes to `ApiKeyAuthenticationHandler`.
2. Otherwise, it routes to standard JWT Bearer authentication (or HttpOnly cookie).

### JWT + HttpOnly Cookie

Login returns a JWT access token and refresh token in the JSON response body. If `EnableCookieAuth` is `true` in settings, the access token is also set as an `HttpOnly + Secure + SameSite=Strict` cookie.

Endpoints accept authentication from **either**:
1. `Authorization: Bearer <token>` header (standard)
2. The configured HttpOnly cookie (fallback for SSR/Razor scenarios)

The cookie is "read-only" from JavaScript's perspective (`HttpOnly` prevents client-side access).

### Refresh Token Rotation

On refresh, the old refresh token is revoked and a new one is issued. This prevents token replay attacks. The refresh token chain is tracked via `ReplacedByToken` for audit.

### API Key Authentication & Governance

Server-to-server integrations, CI/CD tools, and background processes can authenticate via API keys using the `X-API-Key: mwa_<hex>` header.
- **Plaintext secret returned once:** Plaintext keys are prefixed with `mwa_` and returned only once upon creation.
- **SHA-256 hashed storage:** The database only stores the cryptographic SHA-256 hash.
- **Claims & Roles inheritance:** The resulting `ClaimsPrincipal` inherits roles and claims of the owner user, with added `IdentityClaimTypes.ApiKeyId`, `IdentityClaimTypes.ApiKeyName`, and granted `scope` claims. Sensitive administrative claims like `can_manage_api_keys` are never assigned to API keys.
- **Active validation:** Keys that are expired, revoked, or belonging to banned users are rejected automatically. Valid authentications update `LastUsedAt`.
- **Gated Creation (`ManageApiKeys` policy):** Public users cannot create API keys. Key creation is gated by `CanManageApiKeysPolicy`, which requires:
  - Caller must have an interactive user session (JWT or cookie). **API keys cannot create or manage API keys** (anti-recursion).
  - Caller must be System, have `CanManageApiKeys == true` on their profile, possess the `can_manage_api_keys: "true"` claim, or be in a role listed in `IdentitySettings.ApiKeyAllowedRoles`.
- **Dynamic Configuration Controls (`IdentitySettings`):**
  - `EnableApiKeys`: Master toggle to enable/disable API key auth and management.
  - `AllowUserApiKeyCreation`: When `false`, only System administrators can create keys.
  - `ApiKeyAllowedRoles`: List of roles permitted to manage API keys.
  - `MaxApiKeyLifetimeDays`: Upper bound on key lifetime (defaults to 365 days).
  - `RequireKeyExpiration`: Forces callers to supply an explicit expiration date.
  - `MaxKeysPerUser`: Quota on active non-expired, non-revoked keys per user (defaults to 5).
- **Least-Privilege Scopes:** API keys can be provisioned with optional `Scopes` (e.g. `["catalog:read", "orders:write"]`).

### Dummy Users & Recycling

System administrators can create temporary dummy users (`POST /api/v1/identity/users/{id}/dummy`) that clone the profile, roles, and claims of an existing source user for testing and troubleshooting:
- **Temporary Lifetime:** Dummy users expire automatically after a configured duration (defaults to 1440 minutes / 24 hours via `IdentitySettings.DummyUserLifetimeMinutes`, or custom via optional `{ "lifetimeMinutes": 120 }` request body).
- **Credentials Returned:** Returns `DummyUserDto` containing `UserName`, `Email`, `Password`, `ExpiresAt`, `Tokens` (`AuthTokenDto`), and `IsRecycled`.
- **Authentication Enabled:** Dummy users can log in (`POST /api/v1/identity/auth/login`) with their temporary credentials while valid (`DummyExpiresAt > DateTime.UtcNow`). Expired dummy accounts are rejected on login, refresh, and API key auth.
- **Automatic Recycling:** When requesting a new dummy user with `IdentitySettings.RecycleDummyUsers == true`, existing expired dummy users are recycled (password reset, old tokens/keys revoked, new source user roles and claims applied, expiration extended) rather than accumulating dead accounts.
- **Purging:** Administrators can purge all expired dummy users at any time via `DELETE /api/v1/identity/users/dummy/expired`.

### Host Integration

The Host's `Program.cs` must include authentication/authorization middleware. Add these lines after `app.UseKernel()`:

```csharp
app.UseAuthentication();
app.UseAuthorization();
```

---

## 3. API Endpoints

### Auth (`/api/v1/identity/auth`)

| Verb | Path | Auth | Description |
|---|---|---|---|
| `POST` | `/login` | Anonymous | Email + password → AuthTokenDto + cookie |
| `POST` | `/refresh` | Anonymous | Refresh token → new AuthTokenDto + cookie |
| `POST` | `/logout` | Authenticated | Revoke tokens, clear cookie |
| `POST` | `/change-password` | Authenticated | Change current user password + revoke tokens |
| `POST` | `/forgot-password` | Anonymous | Initiate password reset → reset token event |
| `POST` | `/reset-password` | Anonymous | Token + new password → reset password |
| `GET` | `/me` | Authenticated | Current user details (works with JWT or API Key) |

### API Keys (`/api/v1/identity/api-keys`)

| Verb | Path | Auth | Description |
|---|---|---|---|
| `POST` | `/` | ManageApiKeys | Create API key (returns plaintext secret once; interactive session required) |
| `GET` | `/` | ManageApiKeys | List API keys for current user (or filter by `userId` for System) |
| `GET` | `/{id}` | ManageApiKeys | Get API key metadata by ID |
| `DELETE` | `/{id}` | ManageApiKeys | Revoke API key |
| `POST` | `/{id}/revoke` | ManageApiKeys | Revoke API key |

### Users (`/api/v1/identity/users`)

| Verb | Path | Auth | Description |
|---|---|---|---|
| `POST` | `/register` | Anon (public) / System | Create user with profile |
| `GET` | `/` | System | List users (paged, filterable) |
| `GET` | `/{id}` | System / Self | User details |
| `PUT` | `/{id}` | System / Self | Update email/username |
| `PUT` | `/{id}/preferences` | Self | Update preferences JSON |
| `POST` | `/{id}/ban` | System | Ban user + revoke tokens |
| `POST` | `/{id}/unban` | System | Unban user |
| `POST` | `/{id}/dummy` | System | Create or recycle dummy clone of user (returns credentials) |
| `DELETE` | `/dummy/expired` | System | Purge expired dummy users |

### Profiles (`/api/v1/identity/profiles`)

| Verb | Path | Auth | Description |
|---|---|---|---|
| `GET` | `/` | Anonymous | List profiles (public only for anon) |
| `GET` | `/{name}` | Anonymous | Profile details |

---

## 4. Cross-Module Communication

### Contracts (for other modules to reference)

```xml
<!-- In AnotherModule.csproj -->
<ItemGroup>
  <ProjectReference Include="../Identity.Contracts/Identity.Contracts.csproj" />
</ItemGroup>
```

### Query a user from another module:
```csharp
var result = await dispatcher.DispatchAsync(new GetUserSummary(userId), ct);
```

### Check if a user has a role:
```csharp
var result = await dispatcher.DispatchAsync(new ValidateUserAccess(userId, IdentityRoles.Admin), ct);
```

### React to user lifecycle events:
```csharp
public class OnUserCreatedHandler : IEventHandler<UserCreatedEvent>
{
    public Task HandleAsync(UserCreatedEvent @event, CancellationToken ct)
    {
        // Handle user creation in another module
        return Task.CompletedTask;
    }
}
```

---

## 5. Configuration

Settings in `appsettings.Identity.json`:

| Key | Type | Default | Purpose |
|---|---|---|---|
| `JwtSecret` | `string` | `"CHANGE-ME-..."` | HMAC-SHA256 signing key (≥32 chars) |
| `JwtIssuer` | `string` | `"mwa"` | Token issuer |
| `JwtAudience` | `string` | `"mwa"` | Token audience |
| `AccessTokenLifetimeMinutes` | `int` | `60` | Access token TTL |
| `RefreshTokenLifetimeDays` | `int` | `7` | Refresh token TTL |
| `CookieName` | `string` | `"access_token"` | HttpOnly cookie name |
| `EnableCookieAuth` | `bool` | `true` | Set cookie on login/refresh |
| `SeedAdminEmail` | `string` | `"admin@localhost"` | Seed admin email (empty to skip) |
| `SeedAdminPassword` | `string` | `"Admin123!"` | Seed admin password |

---

## 6. Module Activation

Add to `Host/appsettings.Instance.json`:
```json
{
  "ActiveModules": {
    "Identity": true
  }
}
```

Reference the module projects in `Host.csproj`:
```xml
<ItemGroup>
  <ProjectReference Include="..\Modules\Identity\Identity.csproj" />
</ItemGroup>
```

---

## 7. Rules & Best Practices

1. **Profiles are code, not config.** Add new profiles as `IProfile` implementations. They are version-controlled and testable.
2. **Never reference Identity implementation from other modules.** Reference `Identity.Contracts` only.
3. **Always use `DefaultId` for user IDs.** The Identity tables store Ulid as string via value converters.
4. **Use `IScopeContext` for current user info in handlers.** Don't access `HttpContext` directly.
5. **Use `IJsonSerializer`** for preferences serialization — never use `System.Text.Json.JsonSerializer` directly.
6. **Change `JwtSecret` in production.** The default is a placeholder. Use a cryptographically random 64+ character string.
7. **Default users have no role.** Standard users created via public registration have no roles assigned. The only built-in role is `Roles.System` (`"System"`).
8. **By default only two authorization policies:** `Authenticated` and `System`. Prefer using strongly-typed Kernel authorization policies (`[AuthorizePolicy<AuthenticatedPolicy>]` and `[AuthorizePolicy<SystemPolicy>]`) over raw role strings or framework-level `[Authorize]`.
9. **Kernel API versioning on controllers:** Always use `[ApiController]`, `[ApiVersion(1.0)]`, and `[Route("api/v{version:apiVersion}/...")]`.
10. **Endpoint structure:** Enclose each action method in its own `#region ActionName` ... `#endregion` block. Place request body records directly on top of the action method inside the `#region`.
