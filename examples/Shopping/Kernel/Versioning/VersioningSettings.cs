using Asp.Versioning;
using Kernel.Configuration;
using Kernel.Json;

namespace Kernel.Versioning;

/// <summary>
/// Dynamic settings for API versioning, loaded from <c>appsettings.Versioning.json</c>
/// with an optional environment overlay <c>appsettings.Versioning.{Environment}.json</c>.
/// </summary>
public class VersioningSettings : DynamicSettingsSection
{
    /// <inheritdoc />
    public override string SectionName => "Versioning";

    /// <summary>
    /// Default API version formatted as a string (e.g. "1.0"). Default: "1.0".
    /// </summary>
    public string DefaultVersion =>
        TryGetValue<string>(nameof(DefaultVersion), out var val) && !string.IsNullOrWhiteSpace(val) ? val : "1.0";

    /// <summary>
    /// Whether to assume the default API version when a request does not specify one (default: true).
    /// </summary>
    public bool AssumeDefaultVersionWhenUnspecified =>
        !TryGetValue<bool>(nameof(AssumeDefaultVersionWhenUnspecified), out var val) || val;

    /// <summary>
    /// Whether to report supported and deprecated API versions in response headers (default: true).
    /// Emits <c>api-supported-versions</c> and <c>api-deprecated-versions</c> headers.
    /// </summary>
    public bool ReportApiVersions =>
        !TryGetValue<bool>(nameof(ReportApiVersions), out var val) || val;

    /// <summary>
    /// The HTTP header name used for reading API versions from request headers (default: "X-Api-Version").
    /// </summary>
    public string ApiVersionHeader =>
        TryGetValue<string>(nameof(ApiVersionHeader), out var val) && !string.IsNullOrWhiteSpace(val) ? val : "X-Api-Version";

    /// <summary>
    /// The query string parameter name used for reading API versions (default: "api-version").
    /// </summary>
    public string ApiVersionQueryParam =>
        TryGetValue<string>(nameof(ApiVersionQueryParam), out var val) && !string.IsNullOrWhiteSpace(val) ? val : "api-version";

    /// <summary>
    /// Whether URL-segment versioning (/api/v{version}/...) is enabled (default: true).
    /// </summary>
    public bool EnableUrlSegmentVersioning =>
        !TryGetValue<bool>(nameof(EnableUrlSegmentVersioning), out var val) || val;

    /// <summary>
    /// Whether header-based versioning is enabled (default: true).
    /// </summary>
    public bool EnableHeaderVersioning =>
        !TryGetValue<bool>(nameof(EnableHeaderVersioning), out var val) || val;

    /// <summary>
    /// Whether query-parameter-based versioning is enabled (default: true).
    /// </summary>
    public bool EnableQueryVersioning =>
        !TryGetValue<bool>(nameof(EnableQueryVersioning), out var val) || val;

    public VersioningSettings(IJsonSerializer jsonSerializer, string? environment = null)
        : base("appsettings.Versioning.json", jsonSerializer, environment)
    {
    }

    public VersioningSettings(string source, IJsonSerializer jsonSerializer, string? environment = null)
        : base(source, jsonSerializer, environment)
    {
    }

    /// <summary>
    /// Resolves the default <see cref="ApiVersion"/> instance from <see cref="DefaultVersion"/>.
    /// Falls back to 1.0 if parsing fails.
    /// </summary>
    public ApiVersion GetDefaultApiVersion()
    {
        var raw = DefaultVersion;
        if (!string.IsNullOrWhiteSpace(raw) && ApiVersionParser.Default.TryParse(raw, out var apiVersion) && apiVersion != null)
        {
            return apiVersion;
        }

        return new ApiVersion(1, 0);
    }

    /// <summary>
    /// Creates an <see cref="IApiVersionReader"/> configured according to the current settings.
    /// </summary>
    public IApiVersionReader CreateApiVersionReader()
    {
        var readers = new List<IApiVersionReader>();

        if (EnableUrlSegmentVersioning)
        {
            readers.Add(new UrlSegmentApiVersionReader());
        }

        if (EnableHeaderVersioning && !string.IsNullOrWhiteSpace(ApiVersionHeader))
        {
            readers.Add(new HeaderApiVersionReader(ApiVersionHeader));
        }

        if (EnableQueryVersioning && !string.IsNullOrWhiteSpace(ApiVersionQueryParam))
        {
            readers.Add(new QueryStringApiVersionReader(ApiVersionQueryParam));
        }

        return readers.Count switch
        {
            0 => new UrlSegmentApiVersionReader(),
            1 => readers[0],
            _ => ApiVersionReader.Combine(readers.ToArray())
        };
    }
}
