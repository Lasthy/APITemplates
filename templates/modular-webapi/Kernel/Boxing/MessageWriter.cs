using Kernel.Common;
using Kernel.Json;

namespace Kernel.Boxing;

public class MessageWriter(IJsonSerializer _jsonSerializer) : IMessageWriter
{
	public async Task<Result> WriteAsync(Message message, CancellationToken ct = default)
    {
        var existing = await _db.InboxMessages.FirstOrDefaultAsync(m => m.Id == @message.Id, ct);

        if (message is not null)
            return Result.Success("Event has already been added to inbox.");

        message = new InboxMessage
        {
            Id = @message.EventId,
            Type = @message.GetType().FullName!,
            Content = _jsonSerializer.Serialize(@message),
            OccurredAt = @message.OccurredAt,
            Partition = Random.Shared.Next(_config.PartitionStart, _config.PartitionEnd + 1)
        };

        _db.InboxMessages.Add(message);

        await _db.SaveChangesAsync(ct);

        return Result.Success();
    }
}
