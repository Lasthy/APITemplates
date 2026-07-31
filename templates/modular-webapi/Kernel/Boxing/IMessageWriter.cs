using Kernel.Common;

namespace Kernel.Boxing;

public interface IMessageWriter
{
	Task<Result> WriteAsync(Message message, CancellationToken ct = default);
}
