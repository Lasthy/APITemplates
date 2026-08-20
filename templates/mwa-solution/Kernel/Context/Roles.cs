namespace Kernel.Context;

/// <summary>
/// Well-known role constants used for authorization across the application.
/// </summary>
public static class Roles
{
    /// <summary>
    /// The system super-user role. Granted unrestricted access to all operations,
    /// including kernel-level administrative endpoints (health details, dynamic settings,
    /// partition management, etc.).
    /// </summary>
    /// <remarks>
    /// Protect kernel/host-only endpoints with <c>[Authorize(Roles = Roles.System)]</c>
    /// or <c>.RequireAuthorization(p => p.RequireRole(Roles.System))</c> on Minimal API groups.
    /// </remarks>
    public const string System = "System";
}
