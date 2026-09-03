using Kernel.Json;

namespace Kernel.Configuration;

/// <summary>
/// Database and persistence dynamic settings, loaded from <c>appsettings.Data.json</c>
/// with an optional environment overlay <c>appsettings.Data.{Environment}.json</c>.
/// </summary>
public class DataSettings : DynamicSettingsSection
{
    /// <inheritdoc />
    public override string SectionName => "Data";

    /// <summary>
    /// The database provider to use (e.g., Sqlite, SqlServer, PostgreSQL, MySql, InMemory).
    /// </summary>
    public string Provider => GetValue<string>(nameof(Provider));

    /// <summary>
    /// The connection string for the database.
    /// </summary>
    [Sensitive(AllowApiMutation = false)]
    public string ConnectionString => GetValue<string>(nameof(ConnectionString));

    public DataSettings(IJsonSerializer jsonSerializer, string? environment = null)
        : base("appsettings.Data.json", jsonSerializer, environment)
    {
    }
}
