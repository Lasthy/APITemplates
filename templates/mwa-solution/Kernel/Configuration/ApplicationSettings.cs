using Kernel.Json;

namespace Kernel.Configuration;

/// <summary>
/// Application-level dynamic settings, loaded from <c>settings.Application.json</c>
/// with an optional environment overlay <c>settings.Application.{Environment}.json</c>.
/// <para>
/// This is the primary settings section for the application (replaces the former <c>AppSettings</c>).
/// </para>
/// </summary>
public class ApplicationSettings : DynamicSettingsSection
{
    /// <inheritdoc />
    public override string SectionName => "Application";

    /// <summary>
    /// The current application environment name (e.g. <c>"Development"</c>, <c>"Production"</c>).
    /// </summary>
    public string Environment => GetValue<string>(nameof(Environment));

    /// <summary>
    /// The total number of partitions used for distributing outbox messages.
    /// </summary>
    public int PartitionAmount => GetValue<int>(nameof(PartitionAmount));

    public ApplicationSettings(IJsonSerializer jsonSerializer, string? environment = null)
        : base("settings.Application.json", jsonSerializer, environment)
    {
    }
}
