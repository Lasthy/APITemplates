using Kernel.Events;

namespace Kernel.Configuration;

/// <summary>
/// Event raised whenever a <see cref="DynamicSettingsSection"/> value is changed via
/// <see cref="DynamicSettingsSection.SetValue"/>. Published through <see cref="IEventBus"/>
/// so any <see cref="IEventHandler{SettingsChangedEvent}"/> can react to configuration changes.
/// </summary>
/// <param name="SectionName">The <see cref="DynamicSettingsSection.SectionName"/> of the changed section.</param>
/// <param name="Key">The settings key that was modified.</param>
public record SettingsChangedEvent(string SectionName, string Key) : Event;
