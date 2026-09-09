using Asp.Versioning;
using Kernel.Authorization;
using Kernel.Common;
using Kernel.Constants;
using Kernel.Requests;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MultiTenancy.Authorization;
using MultiTenancy.Contracts.Constants;
using MultiTenancy.Contracts.Dtos;
using MultiTenancy.Features.Tenants;
using MultiTenancy.Middleware;
using MvcAuthorize = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;

namespace MultiTenancy.Controllers;

/// <summary>
/// Tenant management endpoints: CRUD operations, suspension/reactivation, and membership lookup.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/multitenancy/tenants")]
[ApiExplorerSettings(GroupName = "MultiTenancy")]
[Tags("Tenants")]
public class TenantsController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public TenantsController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    /// <summary>
    /// Request model for creating a tenant.
    /// </summary>
    public record CreateTenantRequest(string Name, string? Plan = null);

    /// <summary>
    /// Request model for updating a tenant.
    /// </summary>
    public record UpdateTenantRequest(string? Name = null, string? Plan = null, string? MetadataJson = null);

    /// <summary>
    /// Request model for suspending a tenant.
    /// </summary>
    public record SuspendTenantRequest(string? Reason = null);

    /// <summary>
    /// Create a new tenant organization. The authenticated caller becomes the Tenant Owner.
    /// </summary>
    [HttpPost]
    [MvcAuthorize]
    [TenantOptional]
    public async Task<ActionResult<TenantDto>> Create(
        [FromBody] CreateTenantRequest request,
        CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(
            new CreateTenant(request.Name, request.Plan),
            ct);

        return result.Match<ActionResult<TenantDto>>(
            dto => CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto),
            error => BadRequest(new { title = "Failed to create tenant", detail = error.Message }));
    }

    /// <summary>
    /// List all tenants with pagination and optional status filter (System administrators only).
    /// </summary>
    [HttpGet]
    [MvcAuthorize(Roles = Roles.System)]
    [TenantOptional]
    public async Task<ActionResult<PaginatedResult<TenantDto>>> GetAll(
        [FromQuery] TenantStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new GetTenants(status) { Page = page, PageSize = pageSize };
        var result = await _dispatcher.DispatchAsync(query, ct);

        return result.Match<ActionResult<PaginatedResult<TenantDto>>>(
            dto => Ok(dto),
            error => BadRequest(new { title = "Failed to list tenants", detail = error.Message }));
    }

    /// <summary>
    /// List all tenants the current authenticated user belongs to.
    /// </summary>
    [HttpGet("mine")]
    [MvcAuthorize]
    [TenantOptional]
    public async Task<ActionResult<IEnumerable<TenantDto>>> GetMine(CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new GetMyTenants(), ct);

        return result.Match<ActionResult<IEnumerable<TenantDto>>>(
            dto => Ok(dto),
            error => BadRequest(new { title = "Failed to list my tenants", detail = error.Message }));
    }

    /// <summary>
    /// Get details of a specific tenant by ID (TenantMember or System only).
    /// </summary>
    [HttpGet("{id}")]
    [AuthorizePolicy<TenantMemberPolicy>]
    public async Task<ActionResult<TenantDetailDto>> GetById(DefaultId id, CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new GetTenant(id), ct);

        return result.Match<ActionResult<TenantDetailDto>>(
            dto => Ok(dto),
            error => NotFound(new { title = "Tenant not found", detail = error.Message }));
    }

    /// <summary>
    /// Update tenant attributes: name, plan, or metadata (TenantAdmin, TenantOwner, or System).
    /// </summary>
    [HttpPut("{id}")]
    [AuthorizePolicy<TenantAdminPolicy>]
    public async Task<IActionResult> Update(
        DefaultId id,
        [FromBody] UpdateTenantRequest request,
        CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(
            new UpdateTenant(id, request.Name, request.Plan, request.MetadataJson),
            ct);

        return result.Match<IActionResult>(
            _ => NoContent(),
            error => BadRequest(new { title = "Failed to update tenant", detail = error.Message }));
    }

    /// <summary>
    /// Suspend a tenant organization (System administrators only).
    /// </summary>
    [HttpPost("{id}/suspend")]
    [MvcAuthorize(Roles = Roles.System)]
    [TenantOptional]
    public async Task<IActionResult> Suspend(
        DefaultId id,
        [FromBody] SuspendTenantRequest? request,
        CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(
            new SuspendTenant(id, request?.Reason),
            ct);

        return result.Match<IActionResult>(
            _ => NoContent(),
            error => BadRequest(new { title = "Failed to suspend tenant", detail = error.Message }));
    }

    /// <summary>
    /// Reactivate a suspended tenant organization (System administrators only).
    /// </summary>
    [HttpPost("{id}/reactivate")]
    [MvcAuthorize(Roles = Roles.System)]
    [TenantOptional]
    public async Task<IActionResult> Reactivate(DefaultId id, CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new ReactivateTenant(id), ct);

        return result.Match<IActionResult>(
            _ => NoContent(),
            error => BadRequest(new { title = "Failed to reactivate tenant", detail = error.Message }));
    }

    /// <summary>
    /// Soft-delete a tenant organization (TenantOwner or System only).
    /// </summary>
    [HttpDelete("{id}")]
    [AuthorizePolicy<TenantOwnerPolicy>]
    public async Task<IActionResult> Delete(DefaultId id, CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new DeleteTenant(id), ct);

        return result.Match<IActionResult>(
            _ => NoContent(),
            error => BadRequest(new { title = "Failed to delete tenant", detail = error.Message }));
    }
}
