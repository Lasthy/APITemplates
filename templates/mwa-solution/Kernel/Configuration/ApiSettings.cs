namespace Kernel.Configuration;

/// <summary>
/// Settings for the API layer: OpenAPI viewer choice, etc.
/// Bound from the <c>Api</c> section of <c>appsettings.json</c> via <c>IConfiguration</c>.
/// </summary>
public class ApiSettings
{
    public const string SectionName = "Api";

    /// <summary>
    /// The OpenAPI UI viewer to mount.
    /// Accepted values: <c>"scalar"</c> (default) or <c>"swagger"</c>.
    /// </summary>
    public string OpenApiViewer { get; init; } = "scalar";
}
