using Kernel.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MultiTenancy.Configuration;
using MultiTenancy.Persistence;

namespace MultiTenancy.Workers;

/// <summary>
/// Background worker that periodically purges expired or revoked tenant invitations.
/// </summary>
public class InvitationCleanupWorker : BaseWorker
{
    private readonly ILogger<InvitationCleanupWorker> _logger;

    public InvitationCleanupWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<InvitationCleanupWorker> logger,
        TenantSettings settings)
        : base(scopeFactory, logger, TimeSpan.FromMinutes(settings.InvitationCleanupIntervalMinutes > 0 ? settings.InvitationCleanupIntervalMinutes : 60))
    {
        _logger = logger;
    }

    protected override async Task ExecuteJobAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var dbContext = services.GetRequiredService<TenantModuleDbContext>();

        var now = DateTime.UtcNow;
        var staleInvitations = await dbContext.Invitations
            .Where(i => !i.IsAccepted && (i.IsRevoked || i.ExpiresAt <= now))
            .ToListAsync(cancellationToken);

        if (staleInvitations.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Purging {Count} expired or revoked tenant invitations.", staleInvitations.Count);
        dbContext.Invitations.RemoveRange(staleInvitations);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
