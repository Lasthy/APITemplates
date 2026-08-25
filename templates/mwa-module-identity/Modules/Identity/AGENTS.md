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
│   │   ├── UsersController.cs                    # Register, CRUD, ban/unban, preferences, dummy
│   │   └── ProfilesController.cs                 # List/detail profiles
│   ├── Entities/
│   │   ├── ApplicationUser.cs                    # IdentityUser<DefaultId> + profile, preferences, ban, dummy
│   │   ├── ApplicationRole.cs                    # IdentityRole<DefaultId>
│   │   └── RefreshToken.cs                       # Refresh token entity with rotation
│   ├── Features/
│   │   ├── Auth/                                 # Login, RefreshToken, Logout, GetCurrentUser, ChangePassword, ForgotPassword, ResetPassword
│   │   ├── Users/                                # CreateUser, GetUsers, GetUser, UpdateUser, Ban/Unban, Dummy, Preferences
│   │   ├── Profiles/                             # GetProfiles, GetProfile
│   │   ├── GetUserSummaryHandler.cs              # Cross-module handler for GetUserSummary
│   │   └── ValidateUserAccessHandler.cs          # Cross-module handler for ValidateUserAccess
│   ├── Persistence/
│   │   └── IdentityModuleDbContext.cs            # IdentityDbContext with Ulid converters
│   ├── Profiles/
│   │   ├── IProfile.cs                           # Profile contract (static abstract interface)
│   │   ├── ProfileRegistry.cs                    # Singleton registry + ProfileDescriptor
│   │   ├── ProfileCreationContext.cs             # Context for profile creation hooks
│   │   └── DefaultProfiles/
│   │       ├── UserProfile.cs                    # Public user profile
│   │       └── SystemProfile.cs                  # Non-public system profile
│   ├── Services/
│   │   ├── ITokenService.cs / TokenService.cs    # JWT + refresh token generation
│   │   └── IUserService.cs / UserService.cs      # High-level user operations
│   ├── Workers/
│   │   └── RoleSeedWorker.cs                     # One-shot: seeds roles + optional admin user
│   ├── IdentityModule.cs                         # IModule entry point
│   ├── appsettings.Identity.json                 # Module settings (JWT config, seeding)
│   └── appsettings.Identity.Development.json     # Dev overlay
│
└── Identity.Contracts/                           # Companion contracts
    ├── Constants/                                # IdentityRoles, IdentityPolicies, IdentityProfiles, IdentityClaimTypes
    ├── Dtos/                                     # UserDto, UserDetailDto, AuthTokenDto, ProfileDto, ProfileDetailDto
    ├── Events/                                   # UserCreatedEvent, UserBannedEvent, UserUnbannedEvent, PasswordChangedEvent, PasswordResetRequestedEvent, PasswordResetCompletedEvent
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
    public static bool IsPublic => true;
    public static IReadOnlyList<string> Roles => ["User", "TenantAdmin"];
    public static IReadOnlyDictionary<string, string> Claims => new Dictionary<string, string>();
    public static Type? PreferencesType => typeof(TenantPreferences);

    public static async Task OnUserCreatedAsync(ProfileCreationContext context, CancellationToken ct)
    {
        // Create a companion Tenant entity and assign its ID as a claim
        var tenantService = context.Services.GetRequiredService<ITenantService>();
        var tenant = await tenantService.CreateAsync(context.User.Email, ct);
        await context.AddClaimAsync("TenantId", tenant.Id.ToString());
    }
}
```

Key points:
- **`IsPublic`**: If `true`, unauthenticated users can self-register with this profile. If `false`, only admins can create users with this profile.
- **`Roles`**: Automatically assigned on user creation. Roles are seeded at startup by `RoleSeedWorker`.
- **`Claims`**: Static claims assigned on user creation.
- **`PreferencesType`**: If set, the user preferences JSON is validated against this type on update.
- **`OnUserCreatedAsync`**: Hook for side effects (creating companion entities, adding dynamic claims).

### Profile Discovery

All `IProfile` implementations in the Identity module assembly are auto-discovered during `IdentityModule.Register()`. To add profiles from **other modules**, you would need to extend the discovery mechanism (e.g., provide a `ProfileRegistry` extension method).

---

## 2. Authentication Flow

### JWT + HttpOnly Cookie

Login returns a JWT access token and refresh token in the JSON response body. If `EnableCookieAuth` is `true` in settings, the access token is also set as an `HttpOnly + Secure + SameSite=Strict` cookie.

Endpoints accept authentication from **either**:
1. `Authorization: Bearer <token>` header (standard)
2. The configured HttpOnly cookie (fallback for SSR/Razor scenarios)

The cookie is "read-only" from JavaScript's perspective (`HttpOnly` prevents client-side access).

### Refresh Token Rotation

On refresh, the old refresh token is revoked and a new one is issued. This prevents token replay attacks. The refresh token chain is tracked via `ReplacedByToken` for audit.

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
| `GET` | `/me` | Authenticated | Current user details |

### Users (`/api/v1/identity/users`)

| Verb | Path | Auth | Description |
|---|---|---|---|
| `POST` | `/register` | Anon (public) / Admin | Create user with profile |
| `GET` | `/` | Admin | List users (paged, filterable) |
| `GET` | `/{id}` | Admin / Self | User details |
| `PUT` | `/{id}` | Admin / Self | Update email/username |
| `PUT` | `/{id}/preferences` | Self | Update preferences JSON |
| `POST` | `/{id}/ban` | Admin | Ban user + revoke tokens |
| `POST` | `/{id}/unban` | Admin | Unban user |
| `POST` | `/{id}/dummy` | System | Create dummy clone of user |

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
