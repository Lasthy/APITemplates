using Kernel.Messaging;
using Kernel.Persistence;
using Kernel.Testing.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Kernel.Tests.Messaging;

public class OutboxAndInboxTests : KernelTestBase
{
    [Fact]
    public async Task Inbox_MarkProcessedAsync_ShouldRecordAndDetectProcessedMessage()
    {
        await ExecuteDbContextAsync<KernelDbContext>(async db =>
        {
            var logger = NullLogger<Inbox>.Instance;
            var inbox = new Inbox(db, logger);

            var messageId = DefaultId.NewUlid();

            var before = await inbox.HasBeenProcessedAsync(messageId);
            Assert.False(before);

            await inbox.MarkProcessedAsync(messageId);

            var after = await inbox.HasBeenProcessedAsync(messageId);
            Assert.True(after);
        });
    }

    [Fact]
    public async Task Inbox_MarkProcessedAsync_ShouldBeIdempotent()
    {
        await ExecuteDbContextAsync<KernelDbContext>(async db =>
        {
            var logger = NullLogger<Inbox>.Instance;
            var inbox = new Inbox(db, logger);

            var messageId = DefaultId.NewUlid();

            // Calling multiple times should not throw
            await inbox.MarkProcessedAsync(messageId);
            await inbox.MarkProcessedAsync(messageId);

            var count = await db.InboxEntries.CountAsync(e => e.MessageId == messageId);
            Assert.Equal(1, count);
        });
    }

    [Fact]
    public void Message_StateTransitions_ShouldUpdateCorrectly()
    {
        var message = new Message
        {
            Type = "TestEventType",
            Content = "{}",
            Channel = MessageChannel.Event,
            Partition = 0
        };

        Assert.Null(message.ProcessingAt);
        Assert.Null(message.ProcessedAt);
        Assert.Equal(0, message.RetryCount);

        // Claim
        message.MarkProcessing();
        Assert.NotNull(message.ProcessingAt);

        // Fail on error
        message.MarkFailed("Timeout error", TimeSpan.FromSeconds(30));
        Assert.Equal(1, message.RetryCount);
        Assert.NotNull(message.NextRetryAt);
        Assert.Equal("Timeout error", message.LastError);
        Assert.Null(message.ProcessingAt);

        // Mark processed
        message.MarkProcessed();
        Assert.NotNull(message.ProcessedAt);
    }
}
