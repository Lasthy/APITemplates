using Asp.Versioning;
using Kernel.Authorization;
using Kernel.Common;
using Kernel.Constants;
using Kernel.Requests;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MultiTenancy.Authorization;
using MultiTenancy.Contracts.Dtos;
using MultiTenancy.Features.Members;
using MvcAuthorize = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;

namespace MultiTenancy.Controllers;

/// <summary>
/// Tenant member management endpoints: list members, add members, update roles, remove/leave.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/multitenancy/tenants/{tenantId}/members")]
[ApiExplorerSettings(GroupName = "MultiTenancy")]
[Tags("Members")]
public class MembersController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public MembersController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    /// <summary>
    /// Request model for adding a member.
    /// </summary>
    public record AddMemberRequest(DefaultId UserId, string Role);

    /// <summary>
    /// Request model for updating a member's role.
    /// </summary>
    public record UpdateMemberRoleRequest(string Role);

    /// <summary>
    /// List all members of a tenant (TenantMember or System only).
    /// </summary>
    [HttpGet]
    [AuthorizePolicy<TenantMemberPolicy>]
    public async Task<ActionResult<PaginatedResult<TenantMemberDto>>> GetAll(
        DefaultId tenantId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new GetMembers(tenantId) { Page = page, PageSize = pageSize };
        var result = await _dispatcher.DispatchAsync(query, ct);

        return result.Match<ActionResult<PaginatedResult<TenantMemberDto>>>(
            dto => Ok(dto),
            error => BadRequest(new { title = "Failed to list members", detail = error.Message }));
    }

    /// <summary>
    /// Add a user as a member of the tenant (TenantAdmin, TenantOwner, or System).
    /// </summary>
    [HttpPost]
    [AuthorizePolicy<TenantAdminPolicy>]
    public async Task<ActionResult<TenantMemberDto>> Add(
        DefaultId tenantId,
        [FromBody] AddMemberRequest request,
        CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(
            new AddMember(tenantId, request.UserId, request.Role),
            ct);

        return result.Match<ActionResult<TenantMemberDto>>(
            dto => CreatedAtAction(nameof(GetMyMembership), new { tenantId }, dto),
            error => BadRequest(new { title = "Failed to add member", detail = error.Message }));
    }

    /// <summary>
    /// Update a member's role within the tenant (TenantOwner or System only).
    /// </summary>
    [HttpPut("{userId}")]
    [AuthorizePolicy<TenantOwnerPolicy>]
    public async Task<IActionResult> UpdateRole(
        DefaultId tenantId,
        DefaultId userId,
        [FromBody] UpdateMemberRoleRequest request,
        CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(
            new UpdateMemberRole(tenantId, userId, request.Role),
            ct);

        return result.Match<IActionResult>(
            _ => NoContent(),
            error => BadRequest(new { title = "Failed to update member role", detail = error.Message }));
    }

    /// <summary>
    /// Remove a member from the tenant (TenantAdmin, TenantOwner, System, or self-leave).
    /// </summary>
    [HttpDelete("{userId}")]
    [MvcAuthorize]
    public async Task<IActionResult> Remove(
        DefaultId tenantId,
        DefaultId userId,
        CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(
            new RemoveMember(tenantId, userId),
            ct);

        return result.Match<IActionResult>(
            _ => NoContent(),
            error => BadRequest(new { title = "Failed to remove member", detail = error.Message }));
    }

    /// <summary>
    /// Get the current authenticated user's membership details in this tenant.
    /// </summary>
    [HttpGet("me")]
    [AuthorizePolicy<TenantMemberPolicy>]
    public async Task<ActionResult<TenantMemberDto>> GetMyMembership(DefaultId tenantId, CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new GetCurrentMembership(tenantId), ct);

        return result.Match<ActionResult<TenantMemberDto>>(
            dto => Ok(dto),
            error => NotFound(new { title = "Membership not found", detail = error.Message }));
    }
}
