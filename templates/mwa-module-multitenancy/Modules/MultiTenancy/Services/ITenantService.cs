using Kernel.Common;
using MultiTenancy.Entities;

namespace MultiTenancy.Services;

/// <summary>
/// Service providing tenant lifecycle, membership, invitation, and slug operations.
/// </summary>
public interface ITenantService
{
    Task<Result<Tenant>> CreateAsync(string name, DefaultId ownerUserId, string? plan = null, CancellationToken ct = default);
    Task<Result<Tenant>> GetByIdAsync(DefaultId tenantId, CancellationToken ct = default);
    Task<Result<Tenant>> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<Result> UpdateAsync(DefaultId tenantId, string? name, string? plan, string? metadataJson, CancellationToken ct = default);
    Task<Result> SuspendAsync(DefaultId tenantId, string? reason, CancellationToken ct = default);
    Task<Result> ReactivateAsync(DefaultId tenantId, CancellationToken ct = default);
    Task<Result> DeleteAsync(DefaultId tenantId, CancellationToken ct = default);

    // Members
    Task<Result<TenantMember>> AddMemberAsync(DefaultId tenantId, DefaultId userId, string role, CancellationToken ct = default);
    Task<Result> RemoveMemberAsync(DefaultId tenantId, DefaultId userId, CancellationToken ct = default);
    Task<Result> UpdateMemberRoleAsync(DefaultId tenantId, DefaultId userId, string newRole, CancellationToken ct = default);
    Task<Result<IEnumerable<TenantMember>>> GetMembersAsync(DefaultId tenantId, CancellationToken ct = default);

    // Invitations
    Task<Result<TenantInvitation>> InviteAsync(DefaultId tenantId, string email, string role, CancellationToken ct = default);
    Task<Result> AcceptInvitationAsync(string token, DefaultId userId, CancellationToken ct = default);
    Task<Result> RevokeInvitationAsync(DefaultId invitationId, CancellationToken ct = default);

    // Slug generation
    string GenerateSlug(string name);
}
