namespace MultiTenancy.Contracts.Constants;

/// <summary>
/// Claim types associated with multi-tenancy context.
/// </summary>
public static class TenantClaimTypes
{
    public const string TenantId = "tenant_id";
    public const string TenantSlug = "tenant_slug";
    public const string TenantRole = "tenant_role";
}
