using Kernel.Events;

namespace Identity.Contracts.Events;

/// <summary>
/// Domain event published when a user is banned.
/// Other modules can subscribe to revoke active sessions, disable resources, etc.
/// </summary>
public record UserBannedEvent(DefaultId UserId, DefaultId BannedBy, string? Reason) : Event;
