namespace Kernel.AuditLog;

/// <summary>
/// Represents a before-and-after property change within an audit entry.
/// </summary>
/// <param name="PropertyName">The name of the property that was modified.</param>
/// <param name="OldValue">The serialized previous value of the property, or null if added.</param>
/// <param name="NewValue">The serialized current value of the property, or null if deleted.</param>
public record AuditPropertyChange(string PropertyName, string? OldValue, string? NewValue);
