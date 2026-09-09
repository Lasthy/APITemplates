using Kernel.Requests;
using MultiTenancy.Contracts.Dtos;

namespace MultiTenancy.Contracts.Requests;

/// <summary>
/// Cross-module query to retrieve a tenant's summary by ID.
/// </summary>
public record GetTenantSummary(DefaultId TenantId) : IRequest<TenantDto>;
