using Kernel.Requests;

namespace MultiTenancy.Contracts.Requests;

/// <summary>
/// Cross-module query to validate if a user has access (optionally with a required role) to a tenant.
/// </summary>
public record ValidateTenantAccess(
    DefaultId TenantId,
    DefaultId UserId,
    string? RequiredRole = null) : IRequest<bool>;
