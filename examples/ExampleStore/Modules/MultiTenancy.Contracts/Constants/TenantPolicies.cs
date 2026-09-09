namespace MultiTenancy.Contracts.Constants;

/// <summary>
/// Policy names for tenant-level authorization checks.
/// </summary>
public static class TenantPolicies
{
    public const string TenantOwner = "TenantOwner";
    public const string TenantAdmin = "TenantAdmin";
    public const string TenantMember = "TenantMember";
}
