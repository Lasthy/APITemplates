namespace Kernel.Configuration;

public class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// The database provider to use (e.g., Sqlite, SqlServer, PostgreSQL, MySql).
    /// </summary>
    public string Provider { get; set; } = "Sqlite";

    /// <summary>
    /// The connection string for the database.
    /// </summary>
    public string ConnectionString { get; set; } = "Data Source=kernel.db";
}
