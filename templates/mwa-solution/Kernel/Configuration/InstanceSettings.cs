using Kernel.Json;

namespace Kernel.Configuration;

/// <summary>
/// Per-instance dynamic settings, loaded from <c>settings.Instance.json</c>
/// with an optional environment overlay <c>settings.Instance.{Environment}.json</c>.
/// <para>
/// Contains partition range and active module toggles — values that vary per deployment instance.
/// </para>
/// </summary>
public class InstanceSettings : DynamicSettingsSection
{
    /// <inheritdoc />
    public override string SectionName => "Instance";

    /// <summary>
    /// The partition range owned by this instance for outbox message distribution.
    /// </summary>
    public (int Start, int End) Partition
    {
        get => GetValue<(int Start, int End)>(nameof(Partition));
    }

    /// <summary>
    /// Dictionary of module names to their activation status.
    /// <c>true</c> = active, <c>false</c> = inactive.
    /// </summary>
    public Dictionary<string, bool> ActiveModules
    {
        get => GetValue<Dictionary<string, bool>>(nameof(ActiveModules));
    }

    public InstanceSettings(IJsonSerializer jsonSerializer, string? environment = null)
        : base("settings.Instance.json", jsonSerializer, environment)
    {
    }
}
