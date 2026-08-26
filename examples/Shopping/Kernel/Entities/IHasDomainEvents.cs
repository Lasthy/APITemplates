using Kernel.Events;

namespace Kernel.Entities;

public interface IHasDomainEvents
{
    /// <summary>
    /// Domain events produced by the entity.
    /// </summary>
    IReadOnlyCollection<Event> DomainEvents { get; }

    /// <summary>
    /// Clears domain events after they have been dispatched.
    /// </summary>
    void ClearDomainEvents();
}