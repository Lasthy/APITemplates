using Kernel.Boxing;
using Microsoft.EntityFrameworkCore;

namespace Kernel.Persistence;

/// <summary>
/// Database context interface for Kernel infrastructure entities (e.g. outbox messages).
/// Inherits from the common <see cref="IDbContext"/> interface.
/// </summary>
public interface IKernelDbContext : IDbContext
{
    /// <summary>
    /// Outbox/Inbox messages.
    /// </summary>
    DbSet<Message> Messages { get; }
}
