using Kernel.Common;
using Kernel.Context;
using Kernel.Diagnostics;
using Kernel.Json;
using Kernel.Partitioning;
using Kernel.Persistence;

namespace Kernel.Boxing;

/// <summary>
/// The default IMessageWriter implementation.
/// </summary>
public class MessageWriter(
	IJsonSerializer _jsonSerializer,
	IPartitionManager _partitionManager,
	KernelDbContext _db,
	IScopeContext? _scopeContext = null
) : IMessageWriter
{
	/// <inheritdoc/>
	public async Task<Result> WriteAsync(object content, MessageChannel channel, CancellationToken ct = default)
    {	
		try {
			var correlationId = _scopeContext?.CorrelationId
				?? System.Diagnostics.Activity.Current?.GetTagItem(DiagnosticsConstants.CorrelationIdTag)?.ToString();

			var message = new Message {
				Type = content.GetType().AssemblyQualifiedName!,
				Content = _jsonSerializer.Serialize(content),
				Channel = channel,
				Partition = _partitionManager.GetRandomPartition(),
				CorrelationId = correlationId
			};

	        _db.Messages.Add(message);

	        await _db.SaveChangesAsync(ct);

	        return Result.Success();	
		}
		catch(Exception e) {
			return Result.Failure(e);
		}
    }
}

