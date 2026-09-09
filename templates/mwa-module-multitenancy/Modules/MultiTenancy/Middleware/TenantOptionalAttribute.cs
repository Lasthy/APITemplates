namespace MultiTenancy.Middleware;

/// <summary>
/// Marker attribute for endpoints that do not require tenant resolution,
/// even when <c>RequireTenant</c> is configured to <c>true</c> in <see cref="Configuration.TenantSettings"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true)]
public class TenantOptionalAttribute : Attribute
{
}
