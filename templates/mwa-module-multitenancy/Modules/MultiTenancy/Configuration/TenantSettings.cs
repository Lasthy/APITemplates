using Kernel.Configuration;
using Kernel.Json;

namespace MultiTenancy.Configuration;

/// <summary>
/// Dynamic settings section for the MultiTenancy module.
/// Loaded from <c>appsettings.MultiTenancy.json</c> with optional <c>appsettings.MultiTenancy.{Environment}.json</c> overlay.
/// </summary>
public class TenantSettings : DynamicSettingsSection
{
    public override string SectionName => "MultiTenancy";

    /// <summary>
    /// The active resolution strategy: "header", "subdomain", "claim", "route", or "composite".
    /// Defaults to "header".
    /// </summary>
    public string ResolutionStrategy => TryGetValue<string>(nameof(ResolutionStrategy), out var val) && !string.IsNullOrWhiteSpace(val)
        ? val
        : "header";

    /// <summary>
    /// The HTTP header name checked by the header resolution strategy.
    /// Defaults to "X-Tenant-Id".
    /// </summary>
    public string HeaderName => TryGetValue<string>(nameof(HeaderName), out var val) && !string.IsNullOrWhiteSpace(val)
        ? val
        : "X-Tenant-Id";

    /// <summary>
    /// Whether all HTTP requests must successfully resolve a tenant (unless decorated with [TenantOptional]).
    /// Defaults to false.
    /// </summary>
    public bool RequireTenant => TryGetValue<bool>(nameof(RequireTenant), out var val) && val;

    /// <summary>
    /// Maximum number of tenants a single user can create/own.
    /// Defaults to 5.
    /// </summary>
    public int MaxTenantsPerUser => TryGetValue<int>(nameof(MaxTenantsPerUser), out var val) && val > 0
        ? val
        : 5;

    /// <summary>
    /// Maximum number of members allowed in a single tenant.
    /// Defaults to 100.
    /// </summary>
    public int MaxMembersPerTenant => TryGetValue<int>(nameof(MaxMembersPerTenant), out var val) && val > 0
        ? val
        : 100;

    /// <summary>
    /// Number of hours an invitation remains valid before expiring.
    /// Defaults to 72.
    /// </summary>
    public int InvitationExpiryHours => TryGetValue<int>(nameof(InvitationExpiryHours), out var val) && val > 0
        ? val
        : 72;

    /// <summary>
    /// Interval in minutes for the background invitation cleanup worker.
    /// Defaults to 60.
    /// </summary>
    public int InvitationCleanupIntervalMinutes => TryGetValue<int>(nameof(InvitationCleanupIntervalMinutes), out var val) && val > 0
        ? val
        : 60;

    /// <summary>
    /// Default subscription plan/tier assigned to newly created tenants.
    /// Defaults to "free".
    /// </summary>
    public string DefaultPlan => TryGetValue<string>(nameof(DefaultPlan), out var val) && !string.IsNullOrWhiteSpace(val)
        ? val
        : "free";

    /// <summary>
    /// Whether a tenant's URL slug can be modified after creation.
    /// Defaults to false.
    /// </summary>
    public bool AllowSlugChange => TryGetValue<bool>(nameof(AllowSlugChange), out var val) && val;

    /// <summary>
    /// Priority order of resolution strategies evaluated by the composite resolver.
    /// Defaults to ["header", "claim", "subdomain"].
    /// </summary>
    public IReadOnlyList<string> CompositeResolverOrder
    {
        get
        {
            if (TryGetValue<string[]>(nameof(CompositeResolverOrder), out var array) && array is { Length: > 0 })
            {
                return array;
            }
            if (TryGetValue<List<string>>(nameof(CompositeResolverOrder), out var list) && list is { Count: > 0 })
            {
                return list;
            }
            return ["header", "claim", "subdomain"];
        }
    }

    public TenantSettings(IJsonSerializer jsonSerializer, string? environment = null)
        : base("appsettings.MultiTenancy.json", jsonSerializer, environment)
    {
    }
}
