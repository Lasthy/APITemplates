using Kernel.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MultiTenancy.Configuration;
using MultiTenancy.Entities;
using MultiTenancy.Persistence;
using MultiTenancy.Workers;
using Xunit;

namespace MultiTenancy.Tests;

public class InvitationCleanupWorkerTests
{
    [Fact]
    public async Task InvitationCleanupWorker_PurgesExpiredAndRevokedInvitations()
    {
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();

        services.AddDbContext<TenantModuleDbContext>(options =>
            options.UseInMemoryDatabase(dbName));

        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TenantModuleDbContext>();
            var tenant = new Tenant
            {
                Id = DefaultId.NewUlid(),
                Name = "Cleanup Co",
                Slug = "cleanup-co",
                OwnerUserId = DefaultId.NewUlid(),
                CreatedAt = DateTime.UtcNow
            };
            db.Tenants.Add(tenant);

            // 1. Active valid invitation
            db.Invitations.Add(new TenantInvitation
            {
                Id = DefaultId.NewUlid(),
                TenantId = tenant.Id,
                Email = "valid@example.com",
                Role = "Member",
                Token = "valid-token",
                ExpiresAt = DateTime.UtcNow.AddHours(24),
                IsAccepted = false,
                IsRevoked = false,
                CreatedAt = DateTime.UtcNow
            });

            // 2. Expired invitation
            db.Invitations.Add(new TenantInvitation
            {
                Id = DefaultId.NewUlid(),
                TenantId = tenant.Id,
                Email = "expired@example.com",
                Role = "Member",
                Token = "expired-token",
                ExpiresAt = DateTime.UtcNow.AddHours(-1),
                IsAccepted = false,
                IsRevoked = false,
                CreatedAt = DateTime.UtcNow.AddDays(-3)
            });

            // 3. Revoked invitation
            db.Invitations.Add(new TenantInvitation
            {
                Id = DefaultId.NewUlid(),
                TenantId = tenant.Id,
                Email = "revoked@example.com",
                Role = "Member",
                Token = "revoked-token",
                ExpiresAt = DateTime.UtcNow.AddHours(24),
                IsAccepted = false,
                IsRevoked = true,
                CreatedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync();
        }

        var settings = new TenantSettings(new JsonSerializer());
        var logger = NullLogger<InvitationCleanupWorker>.Instance;
        var worker = new TestableInvitationCleanupWorker(scopeFactory, logger, settings);

        await worker.RunJobAsync(CancellationToken.None);

        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TenantModuleDbContext>();
            var remaining = await db.Invitations.ToListAsync();

            Assert.Single(remaining);
            Assert.Equal("valid@example.com", remaining[0].Email);
        }
    }

    private class TestableInvitationCleanupWorker : InvitationCleanupWorker
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public TestableInvitationCleanupWorker(
            IServiceScopeFactory scopeFactory,
            ILogger<InvitationCleanupWorker> logger,
            TenantSettings settings)
            : base(scopeFactory, logger, settings)
        {
            _scopeFactory = scopeFactory;
        }

        public async Task RunJobAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            await ExecuteJobAsync(scope.ServiceProvider, cancellationToken);
        }
    }
}
