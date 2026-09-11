using Kernel.AuditLog;
using Kernel.AuditLog.Features;
using Kernel.Common;
using Kernel.Persistence;
using Kernel.Testing.Infrastructure;
using Xunit;

namespace Kernel.Tests.AuditLog;

public class AuditLogTests : KernelTestBase
{
    [Fact]
    public async Task AuditLogService_ShouldFilterByEntityTypeAndEntityId()
    {
        var userId = DefaultId.NewUlid();

        await ExecuteDbContextAsync<KernelDbContext>(async db =>
        {
            var entry1 = new AuditEntry
            {
                EntityType = "Order",
                EntityId = "101",
                Action = AuditAction.Create,
                UserId = userId,
                Changes = "[]"
            };

            var entry2 = new AuditEntry
            {
                EntityType = "Customer",
                EntityId = "202",
                Action = AuditAction.Update,
                UserId = userId,
                Changes = "[]"
            };

            db.AuditEntries.AddRange(entry1, entry2);
            await db.SaveChangesAsync();

            var service = new AuditLogService(db);

            // Filter for Order 101
            var result = await service.GetEntriesAsync(new GetAuditEntries
            {
                EntityType = "Order",
                EntityId = "101"
            });

            Assert.True(result.IsSuccess);
            Assert.Single(result.Items);
            Assert.Equal("Order", result.Items.First().EntityType);
            Assert.Equal("101", result.Items.First().EntityId);
        });
    }

    [Fact]
    public async Task AuditLogService_ShouldFilterByActionAndUser()
    {
        var user1 = DefaultId.NewUlid();
        var user2 = DefaultId.NewUlid();

        await ExecuteDbContextAsync<KernelDbContext>(async db =>
        {
            db.AuditEntries.AddRange(
                new AuditEntry { EntityType = "Product", EntityId = "1", Action = AuditAction.Create, UserId = user1 },
                new AuditEntry { EntityType = "Product", EntityId = "2", Action = AuditAction.Delete, UserId = user1 },
                new AuditEntry { EntityType = "Product", EntityId = "3", Action = AuditAction.Create, UserId = user2 }
            );
            await db.SaveChangesAsync();

            var service = new AuditLogService(db);

            var result = await service.GetEntriesAsync(new GetAuditEntries
            {
                UserId = user1,
                Action = AuditAction.Delete
            });

            Assert.True(result.IsSuccess);
            Assert.Single(result.Items);
            Assert.Equal("2", result.Items.First().EntityId);
        });
    }
}
