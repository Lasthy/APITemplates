using Kernel.Json;

namespace Kernel.Configuration;

/// <summary>
/// Root host dynamic settings, loaded from <c>appsettings.json</c>
/// with an optional environment overlay <c>appsettings.{Environment}.json</c>.
/// Represents root-level ASP.NET settings such as AllowedHosts and Logging.
/// </summary>
public class HostSettings : DynamicSettingsSection
{
    /// <inheritdoc />
    public override string SectionName => string.Empty;

    /// <summary>
    /// The allowed hosts for the ASP.NET host.
    /// </summary>
    public string AllowedHosts =>
        TryGetValue<string>(nameof(AllowedHosts), out var hosts) && !string.IsNullOrWhiteSpace(hosts)
            ? hosts
            : "*";

    public HostSettings(IJsonSerializer jsonSerializer, string? environment = null)
        : base("appsettings.json", jsonSerializer, environment, sectionName: string.Empty)
    {
    }
}
