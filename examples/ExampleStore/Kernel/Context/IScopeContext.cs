using Kernel.Constants;

namespace Kernel.Context;

/// <summary>
/// Context of the scope in the application.
/// Provides authentication information and user claims.
/// Can be injected into handlers, use cases, and services.
/// </summary>
public interface IScopeContext
{
    /// <summary>
    /// Authenticated user identifier ("sub" or "nameidentifier" claim).
    /// Null if not authenticated.
    /// </summary>
    DefaultId? UserId { get; }

    /// <summary>
    /// Indicates whether the request has an authenticated user.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Authenticated user's roles.
    /// </summary>
    IReadOnlyList<string> Roles { get; }

    /// <summary>
    /// All user claims as a dictionary (type → value).
    /// Claims with multiple values will return only the first value.
    /// Use <see cref="GetClaimValues"/> to get all values for a given claim type.
    /// </summary>
    IReadOnlyDictionary<string, string> Claims { get; }

    /// <summary>
    /// Returns the value of the specified claim, or null if not present.
    /// </summary>
    string? GetClaim(string claimType);

    /// <summary>
    /// Returns all values for the specified claim type.
    /// </summary>
    IReadOnlyList<string> GetClaimValues(string claimType);

    /// <summary>
    /// Checks whether the user belongs to the specified role.
    /// </summary>
    bool IsInRole(string role);

    /// <summary>
    /// Indicates whether the current user holds the <see cref="Roles.System"/> role,
    /// granting unrestricted access to all kernel-level and administrative operations.
    /// </summary>
    bool IsSystem { get; }

    /// <summary>
    /// The correlation identifier for the current operation scope/request.
    /// </summary>
    string? CorrelationId { get; }
}