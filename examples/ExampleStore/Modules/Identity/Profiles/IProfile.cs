namespace Identity.Profiles;

/// <summary>
/// Contract for a user profile definition.
/// Profiles are code-defined (not DB-stored), discovered at startup via assembly scanning,
/// and registered in <see cref="ProfileRegistry"/>. Each profile declares its roles, claims,
/// whether it's public (self-registration allowed), and optional creation rules.
/// </summary>
/// <remarks>
/// <para>
/// Implement this interface with static abstract members. The module startup will scan
/// the assembly for all concrete types implementing <see cref="IProfile"/> and register
/// them as <see cref="ProfileDescriptor"/> entries in the <see cref="ProfileRegistry"/>.
/// </para>
/// <para>
/// <b>Adding new profiles is a code change, not a runtime config change — by design.</b>
/// This ensures profiles are version-controlled, testable, and deterministic.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class TenantProfile : IProfile
/// {
///     public static string Name => "Tenant";
///     public static string Description => "Tenant administrator with org-scoped access.";
///     public static bool IsPublic => true;
///     public static IReadOnlyList&lt;string&gt; Roles => ["User", "TenantAdmin"];
///     public static IReadOnlyDictionary&lt;string, string&gt; Claims => new Dictionary&lt;string, string&gt;();
///     public static Type? PreferencesType => typeof(TenantPreferences);
///     
///     public static async Task OnUserCreatedAsync(ProfileCreationContext context, CancellationToken ct)
///     {
///         // Create companion Tenant entity using additional data passed during registration
///         var orgName = context.GetAdditionalData&lt;string&gt;("OrganizationName") ?? context.User.UserName;
///         var tenantService = context.Services.GetRequiredService&lt;ITenantService&gt;();
///         var tenant = await tenantService.CreateAsync(orgName, ct);
///         await context.AddClaimAsync("TenantId", tenant.Id.ToString());
///     }
/// }
/// </code>
/// </example>
public interface IProfile
{
    /// <summary>
    /// Unique profile identifier (e.g., "User", "Admin", "Tenant").
    /// Used as the key in <see cref="ProfileRegistry"/> and stored on <c>ApplicationUser.ProfileName</c>.
    /// </summary>
    static abstract string Name { get; }

    /// <summary>
    /// Human-readable description of the profile.
    /// </summary>
    static abstract string Description { get; }

    /// <summary>
    /// If <c>true</c>, unauthenticated users can self-register with this profile
    /// via the public registration endpoint.
    /// <para>
    /// If <see cref="CreationRequirements"/> is defined, this property is ignored or reflects
    /// whether any requirement permits public access.
    /// </para>
    /// </summary>
    static virtual bool IsPublic => false;

    /// <summary>
    /// Requirements that must be satisfied by the caller to create a user with this profile.
    /// Defaults to empty, which falls back to <see cref="ProfileRequirements.Public"/> if <see cref="IsPublic"/> is <c>true</c>,
    /// or <see cref="ProfileRequirements.RequireSystem"/> if <see cref="IsPublic"/> is <c>false</c>.
    /// </summary>
    static virtual IReadOnlyList<IProfileCreationRequirement> CreationRequirements => [];

    /// <summary>
    /// Roles automatically assigned to users created with this profile.
    /// </summary>
    static abstract IReadOnlyList<string> Roles { get; }

    /// <summary>
    /// Additional claims automatically assigned to users created with this profile.
    /// Dictionary keys are claim types, values are claim values.
    /// </summary>
    static abstract IReadOnlyDictionary<string, string> Claims { get; }

    /// <summary>
    /// Optional CLR type for user preferences schema validation.
    /// If <c>null</c>, any valid JSON is accepted as preferences.
    /// If set, the preferences JSON must deserialize to this type.
    /// </summary>
    static abstract Type? PreferencesType { get; }

    /// <summary>
    /// Custom creation hook called after the user and roles are persisted but before the
    /// transaction is committed. Use this to create companion entities (e.g., a Tenant,
    /// Organization, or Player entity) and add extra claims (e.g., TenantId).
    /// </summary>
    /// <param name="context">
    /// Provides access to the created user, the <see cref="Microsoft.AspNetCore.Identity.UserManager{T}"/>,
    /// the DI <see cref="IServiceProvider"/> for resolving scoped services, and any additional data
    /// passed by the caller via <see cref="ProfileCreationContext.AdditionalData"/> (with helper methods
    /// like <see cref="ProfileCreationContext.GetAdditionalData{T}"/>).
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    static abstract Task OnUserCreatedAsync(ProfileCreationContext context, CancellationToken ct);

    /// <summary>
    /// If <c>true</c>, users created with this profile are permitted to create and manage their own API keys.
    /// Defaults to <c>false</c>.
    /// </summary>
    static virtual bool CanManageApiKeys => false;
}
