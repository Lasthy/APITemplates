using Kernel.Common;
using Kernel.Json;
using Kernel.Testing.Fakes;
using Microsoft.EntityFrameworkCore;
using MultiTenancy.Configuration;
using MultiTenancy.Contracts.Constants;
using MultiTenancy.Contracts.Events;
using MultiTenancy.Entities;
using MultiTenancy.Persistence;
using MultiTenancy.Services;
using Xunit;

namespace MultiTenancy.Tests;

public class TenantServiceTests
{
    private readonly TenantModuleDbContext _dbContext;
    private readonly TenantSettings _settings;
    private readonly FakeEventBus _eventBus;
    private readonly FakeScopeContext _scopeContext;
    private readonly TenantService _tenantService;

    public TenantServiceTests()
    {
        var options = new DbContextOptionsBuilder<TenantModuleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new TenantModuleDbContext(options);
        _settings = new TenantSettings(new JsonSerializer());
        _eventBus = new FakeEventBus();
        _scopeContext = new FakeScopeContext().AsAuthenticated();

        _tenantService = new TenantService(_dbContext, _settings, _eventBus, _scopeContext);
    }

    [Fact]
    public async Task CreateAsync_CreatesTenantAndOwnerMembership_PublishesEvents()
    {
        var ownerUserId = DefaultId.NewUlid();
        var result = await _tenantService.CreateAsync("Acme Corporation", ownerUserId, "pro");

        Assert.True(result.IsSuccess);
        var tenant = result.Value!;
        Assert.Equal("Acme Corporation", tenant.Name);
        Assert.Equal("acme-corporation", tenant.Slug);
        Assert.Equal(TenantStatus.Active, tenant.Status);
        Assert.Equal("pro", tenant.Plan);
        Assert.Equal(ownerUserId, tenant.OwnerUserId);

        // Verify owner membership created
        var member = await _dbContext.Members
            .FirstOrDefaultAsync(m => m.TenantId == tenant.Id && m.UserId == ownerUserId);
        Assert.NotNull(member);
        Assert.Equal(TenantRoles.Owner, member.Role);

        // Verify events published
        Assert.True(_eventBus.HasPublished<TenantCreatedEvent>(e => e.TenantId == tenant.Id && e.Slug == "acme-corporation"));
        Assert.True(_eventBus.HasPublished<MemberAddedEvent>(e => e.TenantId == tenant.Id && e.UserId == ownerUserId && e.Role == TenantRoles.Owner));
    }

    [Fact]
    public async Task CreateAsync_DuplicateNames_GeneratesUniqueSlugs()
    {
        var ownerUserId = DefaultId.NewUlid();

        var result1 = await _tenantService.CreateAsync("Beta Lab", ownerUserId);
        var result2 = await _tenantService.CreateAsync("Beta Lab", ownerUserId);

        Assert.True(result1.IsSuccess);
        Assert.True(result2.IsSuccess);
        Assert.Equal("beta-lab", result1.Value!.Slug);
        Assert.Equal("beta-lab-2", result2.Value!.Slug);
    }

    [Fact]
    public async Task CreateAsync_ExceedsMaxTenantsPerUser_Fails()
    {
        var ownerUserId = DefaultId.NewUlid();

        for (var i = 0; i < _settings.MaxTenantsPerUser; i++)
        {
            var res = await _tenantService.CreateAsync($"Tenant {i}", ownerUserId);
            Assert.True(res.IsSuccess);
        }

        var failResult = await _tenantService.CreateAsync("One Too Many", ownerUserId);
        Assert.True(failResult.IsFailure);
        Assert.Contains("cannot own more than", failResult.Error!.Message);
    }

    [Fact]
    public async Task SuspendAsync_And_ReactivateAsync_UpdatesStatusAndPublishesEvents()
    {
        var ownerUserId = DefaultId.NewUlid();
        var createResult = await _tenantService.CreateAsync("Suspension Test", ownerUserId);
        var tenantId = createResult.Value!.Id;

        // Suspend
        var suspendResult = await _tenantService.SuspendAsync(tenantId, "Terms of service violation");
        Assert.True(suspendResult.IsSuccess);

        var tenant = await _dbContext.Tenants.FindAsync(tenantId);
        Assert.Equal(TenantStatus.Suspended, tenant!.Status);
        Assert.Equal("Terms of service violation", tenant.SuspensionReason);
        Assert.NotNull(tenant.SuspendedAt);
        Assert.True(_eventBus.HasPublished<TenantSuspendedEvent>(e => e.TenantId == tenantId));

        // Reactivate
        var reactivateResult = await _tenantService.ReactivateAsync(tenantId);
        Assert.True(reactivateResult.IsSuccess);

        var reactivatedTenant = await _dbContext.Tenants.FindAsync(tenantId);
        Assert.Equal(TenantStatus.Active, reactivatedTenant!.Status);
        Assert.Null(reactivatedTenant.SuspensionReason);
        Assert.Null(reactivatedTenant.SuspendedAt);
        Assert.True(_eventBus.HasPublished<TenantReactivatedEvent>(e => e.TenantId == tenantId));
    }

    [Fact]
    public async Task DeleteAsync_SetsStatusDeleted_And_PublishesEvent()
    {
        var ownerUserId = DefaultId.NewUlid();
        var createResult = await _tenantService.CreateAsync("Delete Test", ownerUserId);
        var tenantId = createResult.Value!.Id;

        var deleteResult = await _tenantService.DeleteAsync(tenantId);
        Assert.True(deleteResult.IsSuccess);

        var tenant = await _dbContext.Tenants.FindAsync(tenantId);
        Assert.Equal(TenantStatus.Deleted, tenant!.Status);
        Assert.NotNull(tenant.DeletedAt);
        Assert.True(_eventBus.HasPublished<TenantDeletedEvent>(e => e.TenantId == tenantId));
    }

    [Fact]
    public async Task AddMemberAsync_And_RemoveMemberAsync_Lifecycle()
    {
        var ownerUserId = DefaultId.NewUlid();
        var createResult = await _tenantService.CreateAsync("Team Space", ownerUserId);
        var tenantId = createResult.Value!.Id;

        var newUserId = DefaultId.NewUlid();
        var addResult = await _tenantService.AddMemberAsync(tenantId, newUserId, TenantRoles.Member);
        Assert.True(addResult.IsSuccess);
        Assert.Equal(TenantRoles.Member, addResult.Value!.Role);

        // Duplicate member fails
        var duplicateResult = await _tenantService.AddMemberAsync(tenantId, newUserId, TenantRoles.Admin);
        Assert.True(duplicateResult.IsFailure);

        // Update role
        var updateResult = await _tenantService.UpdateMemberRoleAsync(tenantId, newUserId, TenantRoles.Admin);
        Assert.True(updateResult.IsSuccess);
        var updatedMember = await _dbContext.Members.FirstOrDefaultAsync(m => m.TenantId == tenantId && m.UserId == newUserId);
        Assert.Equal(TenantRoles.Admin, updatedMember!.Role);
        Assert.True(_eventBus.HasPublished<MemberRoleChangedEvent>(e => e.OldRole == TenantRoles.Member && e.NewRole == TenantRoles.Admin));

        // Removing Owner is blocked
        var removeOwnerResult = await _tenantService.RemoveMemberAsync(tenantId, ownerUserId);
        Assert.True(removeOwnerResult.IsFailure);

        // Remove new user succeeds
        var removeResult = await _tenantService.RemoveMemberAsync(tenantId, newUserId);
        Assert.True(removeResult.IsSuccess);
        Assert.False(await _dbContext.Members.AnyAsync(m => m.TenantId == tenantId && m.UserId == newUserId));
    }

    [Fact]
    public async Task Invitation_InviteAndAccept_Success()
    {
        var ownerUserId = DefaultId.NewUlid();
        var createResult = await _tenantService.CreateAsync("Invite Co", ownerUserId);
        var tenantId = createResult.Value!.Id;

        var inviteResult = await _tenantService.InviteAsync(tenantId, "invitee@example.com", TenantRoles.Admin);
        Assert.True(inviteResult.IsSuccess);
        var invitation = inviteResult.Value!;
        Assert.False(invitation.IsAccepted);
        Assert.False(invitation.IsRevoked);
        Assert.NotEmpty(invitation.Token);
        Assert.True(_eventBus.HasPublished<InvitationCreatedEvent>(e => e.Email == "invitee@example.com"));

        // Accept invitation
        var inviteeUserId = DefaultId.NewUlid();
        var acceptResult = await _tenantService.AcceptInvitationAsync(invitation.Token, inviteeUserId);
        Assert.True(acceptResult.IsSuccess);

        // Verify member was added
        var member = await _dbContext.Members.FirstOrDefaultAsync(m => m.TenantId == tenantId && m.UserId == inviteeUserId);
        Assert.NotNull(member);
        Assert.Equal(TenantRoles.Admin, member.Role);

        // Verify invitation is marked accepted
        var updatedInv = await _dbContext.Invitations.FindAsync(invitation.Id);
        Assert.True(updatedInv!.IsAccepted);
        Assert.Equal(inviteeUserId, updatedInv.AcceptedByUserId);
        Assert.True(_eventBus.HasPublished<InvitationAcceptedEvent>(e => e.UserId == inviteeUserId));
    }

    [Fact]
    public async Task Invitation_ExpiredOrRevoked_CannotBeAccepted()
    {
        var ownerUserId = DefaultId.NewUlid();
        var createResult = await _tenantService.CreateAsync("Token Co", ownerUserId);
        var tenantId = createResult.Value!.Id;

        var inviteResult = await _tenantService.InviteAsync(tenantId, "user@example.com", TenantRoles.Member);
        var token = inviteResult.Value!.Token;

        // Revoke
        await _tenantService.RevokeInvitationAsync(inviteResult.Value.Id);

        var acceptResult = await _tenantService.AcceptInvitationAsync(token, DefaultId.NewUlid());
        Assert.True(acceptResult.IsFailure);
        Assert.Contains("revoked", acceptResult.Error!.Message);
    }
}
