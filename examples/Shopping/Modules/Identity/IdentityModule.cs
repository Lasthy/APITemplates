using System.Reflection;
using System.Text;
using Identity.Authentication;
using Identity.Configuration;
using Identity.Contracts.Constants;
using Identity.Controllers;
using Identity.Entities;
using Identity.Persistence;
using Identity.Profiles;
using Identity.Services;
using Identity.Workers;
using Kernel.Configuration;
using Kernel.Json;
using Kernel.Modules;
using Kernel.Persistence.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Identity;

/// <summary>
/// Module registration class for the Identity module.
/// Implements <see cref="IModule"/> to integrate with the Kernel module loading system.
/// </summary>
/// <remarks>
/// This module provides:
/// <list type="bullet">
///   <item>Profile-based user management (code-defined profiles with roles, claims, and creation hooks).</item>
///   <item>ASP.NET Identity with <see cref="DefaultId"/> (Ulid) as the key type.</item>
///   <item>JWT access tokens + refresh token rotation.</item>
///   <item>HttpOnly cookie authentication alongside the standard Authorization header.</item>
///   <item>User preferences (JSON blob with optional per-profile schema validation).</item>
///   <item>Admin user management: ban, unban, create dummy users.</item>
/// </list>
/// <para>
/// To activate this module, set <c>"Identity": true</c> in <c>ActiveModules</c>
/// inside <c>appsettings.Instance.json</c>.
/// </para>
/// </remarks>
public class IdentityModule : IModule
{
    /// <inheritdoc/>
    public static string Name => "Identity";

    /// <inheritdoc/>
    public static void RegisterPolicies(AuthorizationOptions options)
    {
        options.AddPolicy(IdentityPolicies.Client, policy => policy.RequireRole(IdentityRoles.Client, IdentityRoles.System));
        options.AddPolicy(IdentityPolicies.Employee, policy => policy.RequireRole(IdentityRoles.Employee, IdentityRoles.Manager, IdentityRoles.System));
        options.AddPolicy(IdentityPolicies.Manager, policy => policy.RequireRole(IdentityRoles.Manager, IdentityRoles.System));
        options.AddPolicy(IdentityPolicies.System, policy => policy.RequireRole(IdentityRoles.System));
    }

    /// <inheritdoc/>
    public static void Register(IServiceCollection services, IConfiguration configuration)
    {
        // ── Profile registry ──────────────────────────────────────────────────
        var profileRegistry = new ProfileRegistry();
        DiscoverAndRegisterProfiles(profileRegistry, typeof(IdentityModule).Assembly);
        services.AddSingleton(profileRegistry);

        // ── Identity settings ─────────────────────────────────────────────────
        // The DynamicSettingsSection subclass (IdentitySettings) is auto-discovered
        // by the Kernel's AddModule<T>() extension. We also resolve it from DI for
        // the JWT configuration below.
        var dynamicSettings = services
            .Where(d => d.ServiceType == typeof(DynamicSettings))
            .Select(d => d.ImplementationInstance as DynamicSettings)
            .FirstOrDefault();

        IdentitySettings identitySettings;
        if (dynamicSettings != null)
        {
            try { identitySettings = dynamicSettings.GetSection<IdentitySettings>(); }
            catch
            {
                var jsonSerializer = new Kernel.Json.JsonSerializer();
                identitySettings = new IdentitySettings(jsonSerializer, dynamicSettings.Environment);
                dynamicSettings.AddSection(identitySettings);
            }
        }
        else
        {
            identitySettings = new IdentitySettings(new Kernel.Json.JsonSerializer());
        }

        services.AddSingleton(identitySettings);

        // ── Database context ──────────────────────────────────────────────────
        services.AddDbContext<IdentityModuleDbContext>((sp, options) =>
        {
            var ds = sp.GetService<DynamicSettings>();
            DataSettings? dataSettings = null;
            try { dataSettings = ds?.GetSection<DataSettings>(); } catch { }
            dataSettings ??= sp.GetService<DataSettings>();

            if (dataSettings != null)
            {
                options.UseConfiguredDatabase(dataSettings.Provider, dataSettings.ConnectionString, "identity");
            }
            else
            {
                options.UseConfiguredDatabase("sqlite", "Data Source=identity.db", "identity");
            }
            options.AddKernelInterceptors(sp);
        });

        // ── ASP.NET Identity ──────────────────────────────────────────────────
        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                // Password policy
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequiredLength = 8;

                // User settings
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<IdentityModuleDbContext>()
            .AddDefaultTokenProviders();

        // ── Authentication (JWT + API Key) ──────────────────────────────────
        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "IdentityAuth";
                options.DefaultChallengeScheme = "IdentityAuth";
            })
            .AddPolicyScheme("IdentityAuth", "JWT or API Key", options =>
            {
                options.ForwardDefaultSelector = context =>
                {
                    if (context.Request.Headers.ContainsKey(ApiKeyAuthenticationOptions.DefaultScheme) ||
                        context.Request.Headers.ContainsKey("X-API-Key"))
                    {
                        return ApiKeyAuthenticationOptions.DefaultScheme;
                    }
                    return JwtBearerDefaults.AuthenticationScheme;
                };
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = identitySettings.JwtIssuer,
                    ValidAudience = identitySettings.JwtAudience,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(identitySettings.JwtSecret)),
                    ClockSkew = TimeSpan.Zero
                };

                // Cookie fallback: read JWT from HttpOnly cookie if Authorization header is absent
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        if (string.IsNullOrEmpty(context.Token) &&
                            context.Request.Cookies.TryGetValue(identitySettings.CookieName, out var cookie))
                        {
                            context.Token = cookie;
                        }
                        return Task.CompletedTask;
                    }
                };
            })
            .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationOptions.DefaultScheme, options => { });

        // ── Module services ───────────────────────────────────────────────────
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IApiKeyService, ApiKeyService>();

        // ── Controllers ───────────────────────────────────────────────────────
        services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);

        // ── Background workers ────────────────────────────────────────────────
        services.AddHostedService<RoleSeedWorker>();
    }

    /// <inheritdoc/>
    public static void MapEndpoints(IEndpointRouteBuilder app)
    {
        // Controllers are mapped via app.MapControllers() in UseKernel().
        // No additional minimal API endpoints needed.
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Discovers all concrete <see cref="IProfile"/> implementations in the given assembly
    /// and registers them in the <see cref="ProfileRegistry"/>.
    /// </summary>
    private static void DiscoverAndRegisterProfiles(ProfileRegistry registry, Assembly assembly)
    {
        var profileInterface = typeof(IProfile);

        var profileTypes = assembly.GetTypes()
            .Where(t =>
                !t.IsAbstract &&
                !t.IsInterface &&
                t.GetInterfaces().Any(i => i.FullName == profileInterface.FullName))
            .ToList();

        foreach (var profileType in profileTypes)
        {
            // Use reflection to read static abstract members
            var nameProperty = profileType.GetProperty(nameof(IProfile.Name), BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            var descProperty = profileType.GetProperty(nameof(IProfile.Description), BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            var isPublicProperty = profileType.GetProperty(nameof(IProfile.IsPublic), BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            var rolesProperty = profileType.GetProperty(nameof(IProfile.Roles), BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            var claimsProperty = profileType.GetProperty(nameof(IProfile.Claims), BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            var prefsTypeProperty = profileType.GetProperty(nameof(IProfile.PreferencesType), BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            var onCreatedMethod = profileType.GetMethod(nameof(IProfile.OnUserCreatedAsync), BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

            if (nameProperty == null || descProperty == null || isPublicProperty == null ||
                rolesProperty == null || claimsProperty == null || onCreatedMethod == null)
            {
                continue;
            }

            var name = (string)nameProperty.GetValue(null)!;
            var description = (string)descProperty.GetValue(null)!;
            var isPublic = (bool)isPublicProperty.GetValue(null)!;
            var roles = (IReadOnlyList<string>)rolesProperty.GetValue(null)!;
            var claims = (IReadOnlyDictionary<string, string>)claimsProperty.GetValue(null)!;
            var prefsType = (Type?)prefsTypeProperty?.GetValue(null);

            Func<ProfileCreationContext, CancellationToken, Task> onCreated = (ctx, ct) =>
                (Task)onCreatedMethod.Invoke(null, [ctx, ct])!;

            registry.Register(new ProfileDescriptor(
                Name: name,
                Description: description,
                IsPublic: isPublic,
                Roles: roles,
                Claims: claims,
                PreferencesType: prefsType,
                OnUserCreated: onCreated));
        }
    }
}
