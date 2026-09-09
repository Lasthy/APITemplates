using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Kernel.Common;
using Kernel.Context;
using Kernel.Events;
using Microsoft.EntityFrameworkCore;
using MultiTenancy.Configuration;
using MultiTenancy.Contracts.Constants;
using MultiTenancy.Contracts.Events;
using MultiTenancy.Entities;
using MultiTenancy.Persistence;

namespace MultiTenancy.Services;

/// <summary>
/// Default implementation of <see cref="ITenantService"/>.
/// Handles tenant lifecycle, membership management, invitation flows, and event dispatch.
/// </summary>
public class TenantService : ITenantService
{
    private static readonly Regex SlugInvalidCharsRegex = new(@"[^a-z0-9\s-]", RegexOptions.Compiled);
    private static readonly Regex SlugWhitespaceRegex = new(@"[\s-]+", RegexOptions.Compiled);

    private readonly TenantModuleDbContext _dbContext;
    private readonly TenantSettings _settings;
    private readonly IEventBus _eventBus;
    private readonly IScopeContext _scopeContext;

    public TenantService(
        TenantModuleDbContext dbContext,
        TenantSettings settings,
        IEventBus eventBus,
        IScopeContext scopeContext)
    {
        _dbContext = dbContext;
        _settings = settings;
        _eventBus = eventBus;
        _scopeContext = scopeContext;
    }

    public async Task<Result<Tenant>> CreateAsync(
        string name,
        DefaultId ownerUserId,
        string? plan = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<Tenant>(new ArgumentException("Tenant name cannot be empty.", nameof(name)));
        }

        if (ownerUserId == default)
        {
            return Result.Failure<Tenant>(new ArgumentException("Owner user ID is required.", nameof(ownerUserId)));
        }

        // Limit check
        var userTenantCount = await _dbContext.Tenants
            .CountAsync(t => t.OwnerUserId == ownerUserId && t.Status != TenantStatus.Deleted, ct);

        if (userTenantCount >= _settings.MaxTenantsPerUser)
        {
            return Result.Failure<Tenant>(new InvalidOperationException(
                $"User cannot own more than {_settings.MaxTenantsPerUser} tenants."));
        }

        // Generate unique slug
        var baseSlug = GenerateSlug(name);
        var slug = baseSlug;
        var suffix = 2;

        while (await _dbContext.Tenants.AnyAsync(t => t.Slug == slug, ct))
        {
            slug = $"{baseSlug}-{suffix++}";
        }

        var tenant = new Tenant
        {
            Id = DefaultId.NewUlid(),
            Name = name.Trim(),
            Slug = slug,
            Status = TenantStatus.Active,
            Plan = plan ?? _settings.DefaultPlan,
            OwnerUserId = ownerUserId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _scopeContext.UserId
        };

        _dbContext.Tenants.Add(tenant);

        // Add creator as tenant Owner
        var ownerMember = new TenantMember
        {
            Id = DefaultId.NewUlid(),
            TenantId = tenant.Id,
            UserId = ownerUserId,
            Role = TenantRoles.Owner,
            JoinedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _scopeContext.UserId
        };

        _dbContext.Members.Add(ownerMember);

        await _dbContext.SaveChangesAsync(ct);

        await _eventBus.PublishAsync(
            new TenantCreatedEvent(tenant.Id, tenant.Name, tenant.Slug, ownerUserId),
            ct);

        await _eventBus.PublishAsync(
            new MemberAddedEvent(tenant.Id, ownerUserId, TenantRoles.Owner),
            ct);

        return Result.Success(tenant);
    }

    public async Task<Result<Tenant>> GetByIdAsync(DefaultId tenantId, CancellationToken ct = default)
    {
        var tenant = await _dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId && t.Status != TenantStatus.Deleted, ct);

        if (tenant == null)
        {
            return Result.Failure<Tenant>(new KeyNotFoundException($"Tenant '{tenantId}' not found."));
        }

        return Result.Success(tenant);
    }

    public async Task<Result<Tenant>> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return Result.Failure<Tenant>(new ArgumentException("Slug cannot be empty.", nameof(slug)));
        }

        var normalized = slug.Trim().ToLowerInvariant();
        var tenant = await _dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Slug == normalized && t.Status != TenantStatus.Deleted, ct);

        if (tenant == null)
        {
            return Result.Failure<Tenant>(new KeyNotFoundException($"Tenant with slug '{slug}' not found."));
        }

        return Result.Success(tenant);
    }

    public async Task<Result> UpdateAsync(
        DefaultId tenantId,
        string? name,
        string? plan,
        string? metadataJson,
        CancellationToken ct = default)
    {
        var tenant = await _dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId && t.Status != TenantStatus.Deleted, ct);
        if (tenant == null)
        {
            return Result.Failure(new KeyNotFoundException($"Tenant '{tenantId}' not found."));
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            tenant.Name = name.Trim();

            if (_settings.AllowSlugChange)
            {
                var baseSlug = GenerateSlug(name);
                var slug = baseSlug;
                var suffix = 2;

                while (await _dbContext.Tenants.AnyAsync(t => t.Slug == slug && t.Id != tenantId, ct))
                {
                    slug = $"{baseSlug}-{suffix++}";
                }

                tenant.Slug = slug;
            }
        }

        if (plan != null)
        {
            tenant.Plan = plan;
        }

        if (metadataJson != null)
        {
            tenant.MetadataJson = metadataJson;
        }

        tenant.UpdatedAt = DateTime.UtcNow;
        tenant.UpdatedBy = _scopeContext.UserId;

        await _dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> SuspendAsync(DefaultId tenantId, string? reason, CancellationToken ct = default)
    {
        var tenant = await _dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId && t.Status != TenantStatus.Deleted, ct);
        if (tenant == null)
        {
            return Result.Failure(new KeyNotFoundException($"Tenant '{tenantId}' not found."));
        }

        if (tenant.Status == TenantStatus.Suspended)
        {
            return Result.Failure(new InvalidOperationException("Tenant is already suspended."));
        }

        tenant.Status = TenantStatus.Suspended;
        tenant.SuspensionReason = reason;
        tenant.SuspendedAt = DateTime.UtcNow;
        tenant.SuspendedBy = _scopeContext.UserId;
        tenant.UpdatedAt = DateTime.UtcNow;
        tenant.UpdatedBy = _scopeContext.UserId;

        await _dbContext.SaveChangesAsync(ct);

        await _eventBus.PublishAsync(
            new TenantSuspendedEvent(tenantId, reason, _scopeContext.UserId),
            ct);

        return Result.Success();
    }

    public async Task<Result> ReactivateAsync(DefaultId tenantId, CancellationToken ct = default)
    {
        var tenant = await _dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId && t.Status != TenantStatus.Deleted, ct);
        if (tenant == null)
        {
            return Result.Failure(new KeyNotFoundException($"Tenant '{tenantId}' not found."));
        }

        if (tenant.Status != TenantStatus.Suspended)
        {
            return Result.Failure(new InvalidOperationException("Tenant is not suspended."));
        }

        tenant.Status = TenantStatus.Active;
        tenant.SuspensionReason = null;
        tenant.SuspendedAt = null;
        tenant.SuspendedBy = null;
        tenant.UpdatedAt = DateTime.UtcNow;
        tenant.UpdatedBy = _scopeContext.UserId;

        await _dbContext.SaveChangesAsync(ct);

        await _eventBus.PublishAsync(
            new TenantReactivatedEvent(tenantId, _scopeContext.UserId),
            ct);

        return Result.Success();
    }

    public async Task<Result> DeleteAsync(DefaultId tenantId, CancellationToken ct = default)
    {
        var tenant = await _dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId && t.Status != TenantStatus.Deleted, ct);
        if (tenant == null)
        {
            return Result.Failure(new KeyNotFoundException($"Tenant '{tenantId}' not found."));
        }

        tenant.Status = TenantStatus.Deleted;
        tenant.DeletedAt = DateTime.UtcNow;
        tenant.DeletedBy = _scopeContext.UserId;
        tenant.UpdatedAt = DateTime.UtcNow;
        tenant.UpdatedBy = _scopeContext.UserId;

        await _dbContext.SaveChangesAsync(ct);

        await _eventBus.PublishAsync(
            new TenantDeletedEvent(tenantId, _scopeContext.UserId),
            ct);

        return Result.Success();
    }

    public async Task<Result<TenantMember>> AddMemberAsync(
        DefaultId tenantId,
        DefaultId userId,
        string role,
        CancellationToken ct = default)
    {
        var tenant = await _dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId && t.Status != TenantStatus.Deleted, ct);
        if (tenant == null)
        {
            return Result.Failure<TenantMember>(new KeyNotFoundException($"Tenant '{tenantId}' not found."));
        }

        if (tenant.Status == TenantStatus.Suspended)
        {
            return Result.Failure<TenantMember>(new InvalidOperationException("Cannot add members to a suspended tenant."));
        }

        var memberCount = await _dbContext.Members.CountAsync(m => m.TenantId == tenantId, ct);
        if (memberCount >= _settings.MaxMembersPerTenant)
        {
            return Result.Failure<TenantMember>(new InvalidOperationException(
                $"Tenant has reached the maximum member limit of {_settings.MaxMembersPerTenant}."));
        }

        var exists = await _dbContext.Members.AnyAsync(m => m.TenantId == tenantId && m.UserId == userId, ct);
        if (exists)
        {
            return Result.Failure<TenantMember>(new InvalidOperationException("User is already a member of this tenant."));
        }

        var member = new TenantMember
        {
            Id = DefaultId.NewUlid(),
            TenantId = tenantId,
            UserId = userId,
            Role = string.IsNullOrWhiteSpace(role) ? TenantRoles.Member : role.Trim(),
            JoinedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _scopeContext.UserId
        };

        _dbContext.Members.Add(member);
        await _dbContext.SaveChangesAsync(ct);

        await _eventBus.PublishAsync(
            new MemberAddedEvent(tenantId, userId, member.Role),
            ct);

        return Result.Success(member);
    }

    public async Task<Result> RemoveMemberAsync(DefaultId tenantId, DefaultId userId, CancellationToken ct = default)
    {
        var member = await _dbContext.Members.FirstOrDefaultAsync(m => m.TenantId == tenantId && m.UserId == userId, ct);
        if (member == null)
        {
            return Result.Failure(new KeyNotFoundException($"Membership for user '{userId}' in tenant '{tenantId}' not found."));
        }

        var tenant = await _dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId && t.Status != TenantStatus.Deleted, ct);
        if (tenant != null && tenant.OwnerUserId == userId)
        {
            return Result.Failure(new InvalidOperationException("Cannot remove the tenant owner. Transfer ownership first."));
        }

        _dbContext.Members.Remove(member);
        await _dbContext.SaveChangesAsync(ct);

        await _eventBus.PublishAsync(
            new MemberRemovedEvent(tenantId, userId, _scopeContext.UserId),
            ct);

        return Result.Success();
    }

    public async Task<Result> UpdateMemberRoleAsync(
        DefaultId tenantId,
        DefaultId userId,
        string newRole,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(newRole))
        {
            return Result.Failure(new ArgumentException("Role cannot be empty.", nameof(newRole)));
        }

        var member = await _dbContext.Members.FirstOrDefaultAsync(m => m.TenantId == tenantId && m.UserId == userId, ct);
        if (member == null)
        {
            return Result.Failure(new KeyNotFoundException($"Membership for user '{userId}' in tenant '{tenantId}' not found."));
        }

        var oldRole = member.Role;
        if (string.Equals(oldRole, newRole, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Success();
        }

        // If transferring ownership to this user
        if (string.Equals(newRole, TenantRoles.Owner, StringComparison.OrdinalIgnoreCase))
        {
            var tenant = await _dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct);
            if (tenant != null)
            {
                // Demote old owner to Admin
                var oldOwner = await _dbContext.Members.FirstOrDefaultAsync(m => m.TenantId == tenantId && m.UserId == tenant.OwnerUserId, ct);
                if (oldOwner != null && oldOwner.UserId != userId)
                {
                    oldOwner.Role = TenantRoles.Admin;
                    oldOwner.UpdatedAt = DateTime.UtcNow;
                    oldOwner.UpdatedBy = _scopeContext.UserId;
                }

                tenant.OwnerUserId = userId;
                tenant.UpdatedAt = DateTime.UtcNow;
                tenant.UpdatedBy = _scopeContext.UserId;
            }
        }

        member.Role = newRole.Trim();
        member.UpdatedAt = DateTime.UtcNow;
        member.UpdatedBy = _scopeContext.UserId;

        await _dbContext.SaveChangesAsync(ct);

        await _eventBus.PublishAsync(
            new MemberRoleChangedEvent(tenantId, userId, oldRole, member.Role),
            ct);

        return Result.Success();
    }

    public async Task<Result<IEnumerable<TenantMember>>> GetMembersAsync(DefaultId tenantId, CancellationToken ct = default)
    {
        var exists = await _dbContext.Tenants.AnyAsync(t => t.Id == tenantId && t.Status != TenantStatus.Deleted, ct);
        if (!exists)
        {
            return Result.Failure<IEnumerable<TenantMember>>(new KeyNotFoundException($"Tenant '{tenantId}' not found."));
        }

        var members = await _dbContext.Members
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId)
            .OrderBy(m => m.JoinedAt)
            .ToListAsync(ct);

        return Result.Success<IEnumerable<TenantMember>>(members);
    }

    public async Task<Result<TenantInvitation>> InviteAsync(
        DefaultId tenantId,
        string email,
        string role,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Result.Failure<TenantInvitation>(new ArgumentException("Email cannot be empty.", nameof(email)));
        }

        var tenant = await _dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId && t.Status != TenantStatus.Deleted, ct);
        if (tenant == null)
        {
            return Result.Failure<TenantInvitation>(new KeyNotFoundException($"Tenant '{tenantId}' not found."));
        }

        if (tenant.Status == TenantStatus.Suspended)
        {
            return Result.Failure<TenantInvitation>(new InvalidOperationException("Cannot invite users to a suspended tenant."));
        }

        var memberCount = await _dbContext.Members.CountAsync(m => m.TenantId == tenantId, ct);
        if (memberCount >= _settings.MaxMembersPerTenant)
        {
            return Result.Failure<TenantInvitation>(new InvalidOperationException(
                $"Tenant has reached the maximum member limit of {_settings.MaxMembersPerTenant}."));
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();

        // Revoke any previous pending invitations for this email in this tenant
        var existingPending = await _dbContext.Invitations
            .Where(i => i.TenantId == tenantId && i.Email == normalizedEmail && !i.IsAccepted && !i.IsRevoked)
            .ToListAsync(ct);

        foreach (var inv in existingPending)
        {
            inv.IsRevoked = true;
            inv.UpdatedAt = DateTime.UtcNow;
            inv.UpdatedBy = _scopeContext.UserId;
        }

        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToHexString(tokenBytes).ToLowerInvariant();

        var invitation = new TenantInvitation
        {
            Id = DefaultId.NewUlid(),
            TenantId = tenantId,
            Email = normalizedEmail,
            Role = string.IsNullOrWhiteSpace(role) ? TenantRoles.Member : role.Trim(),
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddHours(_settings.InvitationExpiryHours),
            IsAccepted = false,
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _scopeContext.UserId
        };

        _dbContext.Invitations.Add(invitation);
        await _dbContext.SaveChangesAsync(ct);

        await _eventBus.PublishAsync(
            new InvitationCreatedEvent(tenantId, normalizedEmail, invitation.Role, _scopeContext.UserId),
            ct);

        return Result.Success(invitation);
    }

    public async Task<Result> AcceptInvitationAsync(string token, DefaultId userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Result.Failure(new ArgumentException("Token cannot be empty.", nameof(token)));
        }

        var normalizedToken = token.Trim().ToLowerInvariant();
        var invitation = await _dbContext.Invitations
            .Include(i => i.Tenant)
            .FirstOrDefaultAsync(i => i.Token == normalizedToken, ct);

        if (invitation == null)
        {
            return Result.Failure(new KeyNotFoundException("Invitation not found."));
        }

        if (invitation.IsRevoked)
        {
            return Result.Failure(new InvalidOperationException("Invitation has been revoked."));
        }

        if (invitation.IsAccepted)
        {
            return Result.Failure(new InvalidOperationException("Invitation has already been accepted."));
        }

        if (invitation.ExpiresAt <= DateTime.UtcNow)
        {
            return Result.Failure(new InvalidOperationException("Invitation has expired."));
        }

        if (invitation.Tenant.Status != TenantStatus.Active)
        {
            return Result.Failure(new InvalidOperationException("Tenant is not active."));
        }

        // Add member
        var addResult = await AddMemberAsync(invitation.TenantId, userId, invitation.Role, ct);
        if (addResult.IsFailure)
        {
            return addResult;
        }

        invitation.IsAccepted = true;
        invitation.AcceptedAt = DateTime.UtcNow;
        invitation.AcceptedByUserId = userId;
        invitation.UpdatedAt = DateTime.UtcNow;
        invitation.UpdatedBy = userId;

        await _dbContext.SaveChangesAsync(ct);

        await _eventBus.PublishAsync(
            new InvitationAcceptedEvent(invitation.TenantId, userId, invitation.Id),
            ct);

        return Result.Success();
    }

    public async Task<Result> RevokeInvitationAsync(DefaultId invitationId, CancellationToken ct = default)
    {
        var invitation = await _dbContext.Invitations.FirstOrDefaultAsync(i => i.Id == invitationId, ct);
        if (invitation == null)
        {
            return Result.Failure(new KeyNotFoundException($"Invitation '{invitationId}' not found."));
        }

        if (invitation.IsAccepted)
        {
            return Result.Failure(new InvalidOperationException("Accepted invitations cannot be revoked."));
        }

        invitation.IsRevoked = true;
        invitation.UpdatedAt = DateTime.UtcNow;
        invitation.UpdatedBy = _scopeContext.UserId;

        await _dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }

    public string GenerateSlug(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "tenant";
        }

        var lower = name.Trim().ToLowerInvariant();
        var cleaned = SlugInvalidCharsRegex.Replace(lower, "");
        var slug = SlugWhitespaceRegex.Replace(cleaned, "-").Trim('-');

        return string.IsNullOrWhiteSpace(slug) ? "tenant" : slug;
    }
}
