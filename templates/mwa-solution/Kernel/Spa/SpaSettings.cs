using Kernel.Configuration;
using Kernel.Json;

namespace Kernel.Spa;

/// <summary>
/// Dynamic settings for Single Page Application (SPA) serving, loaded from <c>appsettings.Spa.json</c>
/// with optional environment overlay <c>appsettings.Spa.{Environment}.json</c>.
/// </summary>
public class SpaSettings : DynamicSettingsSection
{
    private static readonly string[] DefaultExcludedPrefixes =
    [
        "/api",
        "/openapi",
        "/scalar",
        "/docs",
        "/hubs",
        "/health",
        "/files"
    ];

    /// <inheritdoc />
    public override string SectionName => "Spa";

    /// <summary>
    /// Whether SPA serving and reverse proxying are enabled globally. Defaults to <c>true</c>.
    /// </summary>
    public bool Enabled =>
        !TryGetValue<bool>(nameof(Enabled), out var val) || val;

    /// <summary>
    /// Maximum time in seconds to wait for reverse proxy responses from SPA dev servers. Defaults to 30s.
    /// </summary>
    public int ProxyTimeoutSeconds =>
        TryGetValue<int>(nameof(ProxyTimeoutSeconds), out var val) && val > 0 ? val : 30;

    /// <summary>
    /// URL prefixes that must never be intercepted by the SPA reverse proxy or fallback routing
    /// (e.g. API controllers, OpenAPI specs, Swagger/Scalar UIs, SignalR hubs).
    /// </summary>
    public string[] ExcludedPrefixes
    {
        get
        {
            if (TryGetValue<string[]>(nameof(ExcludedPrefixes), out var prefixes) && prefixes != null && prefixes.Length > 0)
            {
                return prefixes;
            }
            return DefaultExcludedPrefixes;
        }
    }

    /// <summary>
    /// Dictionary of configured Host-level SPA applications, keyed by application name.
    /// </summary>
    public Dictionary<string, SpaAppOptions> Apps
    {
        get
        {
            if (TryGetValue<Dictionary<string, SpaAppOptions>>(nameof(Apps), out var apps) && apps != null)
            {
                // Ensure name and owner are set if omitted in JSON
                foreach (var (key, app) in apps)
                {
                    if (string.IsNullOrWhiteSpace(app.Name) || app.Name == "Web")
                    {
                        typeof(SpaAppOptions).GetProperty(nameof(SpaAppOptions.Name))?.SetValue(app, key);
                    }
                    if (string.IsNullOrWhiteSpace(app.Owner))
                    {
                        typeof(SpaAppOptions).GetProperty(nameof(SpaAppOptions.Owner))?.SetValue(app, "Host");
                    }
                }
                return apps;
            }
            return [];
        }
    }

    public SpaSettings(IJsonSerializer jsonSerializer, string? environment = null)
        : base("appsettings.Spa.json", jsonSerializer, environment)
    {
    }

    public SpaSettings(string source, IJsonSerializer jsonSerializer, string? environment = null)
        : base(source, jsonSerializer, environment)
    {
    }
}
