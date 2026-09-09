using Asp.Versioning;
using Kernel.Authorization;
using Kernel.Common;
using Kernel.Requests;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MultiTenancy.Authorization;
using MultiTenancy.Contracts.Dtos;
using MultiTenancy.Features.Invitations;
using MultiTenancy.Middleware;
using MvcAuthorize = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;

namespace MultiTenancy.Controllers;

/// <summary>
/// Tenant invitation endpoints: invite users by email, list pending invitations, revoke, and accept.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/multitenancy/tenants/{tenantId}/invitations")]
[ApiExplorerSettings(GroupName = "MultiTenancy")]
[Tags("Invitations")]
public class InvitationsController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public InvitationsController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    /// <summary>
    /// Request model for sending an invitation.
    /// </summary>
    public record CreateInvitationRequest(string Email, string Role);

    /// <summary>
    /// Request model for accepting an invitation.
    /// </summary>
    public record AcceptInvitationRequest(string Token);

    /// <summary>
    /// Send an invitation to an email address to join the tenant (TenantAdmin, TenantOwner, or System).
    /// </summary>
    [HttpPost]
    [AuthorizePolicy<TenantAdminPolicy>]
    public async Task<ActionResult<TenantInvitationDto>> Invite(
        DefaultId tenantId,
        [FromBody] CreateInvitationRequest request,
        CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(
            new CreateInvitation(tenantId, request.Email, request.Role),
            ct);

        return result.Match<ActionResult<TenantInvitationDto>>(
            dto => Ok(dto),
            error => BadRequest(new { title = "Failed to create invitation", detail = error.Message }));
    }

    /// <summary>
    /// List all pending invitations for a tenant (TenantAdmin, TenantOwner, or System).
    /// </summary>
    [HttpGet]
    [AuthorizePolicy<TenantAdminPolicy>]
    public async Task<ActionResult<IEnumerable<TenantInvitationDto>>> GetAll(
        DefaultId tenantId,
        CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new GetInvitations(tenantId), ct);

        return result.Match<ActionResult<IEnumerable<TenantInvitationDto>>>(
            dto => Ok(dto),
            error => BadRequest(new { title = "Failed to list invitations", detail = error.Message }));
    }

    /// <summary>
    /// Revoke a pending invitation (TenantAdmin, TenantOwner, or System).
    /// </summary>
    [HttpDelete("{id}")]
    [AuthorizePolicy<TenantAdminPolicy>]
    public async Task<IActionResult> Revoke(
        DefaultId tenantId,
        DefaultId id,
        CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new RevokeInvitation(id), ct);

        return result.Match<IActionResult>(
            _ => NoContent(),
            error => BadRequest(new { title = "Failed to revoke invitation", detail = error.Message }));
    }

    /// <summary>
    /// Accept a tenant invitation using a secure invitation token (Authenticated user, tenant-optional).
    /// </summary>
    [HttpPost("accept")]
    [MvcAuthorize]
    [TenantOptional]
    public async Task<IActionResult> Accept(
        DefaultId tenantId,
        [FromBody] AcceptInvitationRequest request,
        CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new AcceptInvitation(request.Token), ct);

        return result.Match<IActionResult>(
            _ => Ok(new { success = true }),
            error => BadRequest(new { title = "Failed to accept invitation", detail = error.Message }));
    }
}
