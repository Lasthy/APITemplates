namespace Kernel.Spa;

/// <summary>
/// Configuration options for a single Single Page Application (SPA).
/// Can be owned by the Host (e.g. root storefront at <c>/</c>) or by an individual Module (e.g. Identity at <c>/auth</c>).
/// </summary>
public record SpaAppOptions
{
    private string _basePath = "/";

    /// <summary>
    /// Unique identifier for this SPA (e.g. "Web", "Identity.Auth", "Identity.Admin").
    /// </summary>
    public string Name { get; init; } = "Web";

    /// <summary>
    /// The owner of this SPA (e.g. "Host" or module name such as "Identity").
    /// </summary>
    public string Owner { get; init; } = "Host";

    /// <summary>
    /// The URL base path / route prefix where this SPA is mounted (e.g. "/", "/auth", "/admin").
    /// Always normalized to start with a slash and have no trailing slash (unless it is root "/").
    /// </summary>
    public string BasePath
    {
        get => _basePath;
        init => _basePath = NormalizeBasePath(value);
    }

    /// <summary>
    /// The target URL of the frontend development server (e.g. "http://localhost:5173").
    /// In development, unhandled requests under <see cref="BasePath"/> are reverse-proxied here.
    /// </summary>
    public string? DevServerUrl { get; init; }

    /// <summary>
    /// Relative or absolute path to the SPA source code directory (e.g. "ClientApp" or "Modules/Identity/ClientApp").
    /// Used for diagnostics and dev server instructions.
    /// </summary>
    public string SourcePath { get; init; } = "ClientApp";

    /// <summary>
    /// Relative or absolute path to the compiled static assets directory (e.g. "ClientApp/dist" or "Modules/Identity/ClientApp/dist").
    /// In production, static files are served from this folder.
    /// </summary>
    public string DistPath { get; init; } = "ClientApp/dist";

    /// <summary>
    /// The SPA entry point file (default: "index.html") served for unhandled routes within <see cref="BasePath"/>.
    /// </summary>
    public string FallbackFile { get; init; } = "index.html";

    /// <summary>
    /// Whether this SPA is enabled. Defaults to <c>true</c>.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// When <c>true</c>, strips <see cref="BasePath"/> from the request path before proxying to <see cref="DevServerUrl"/>.
    /// Defaults to <c>false</c>, preserving the full path (recommended for Vite/Webpack configured with a base path).
    /// </summary>
    public bool StripBasePathOnProxy { get; init; } = false;

    /// <summary>
    /// Optional custom command to run the frontend development server in development mode
    /// (e.g. "npm run dev", "pnpm dev", "yarn dev", "bun run dev").
    /// </summary>
    public string? DevServerCommand { get; init; }

    /// <summary>
    /// Whether to automatically execute <see cref="DevServerCommand"/> in <see cref="SourcePath"/>
    /// when the backend starts in Development mode. Defaults to <c>false</c>.
    /// The process tree is automatically terminated when the backend application stops.
    /// </summary>
    public bool AutoStartDevServer { get; init; } = false;

    /// <summary>
    /// Normalizes a base path string so it starts with '/' and does not end with '/' unless it is root '/'.
    /// </summary>
    public static string NormalizeBasePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path == "/")
        {
            return "/";
        }

        var trimmed = path.Trim();
        if (!trimmed.StartsWith('/'))
        {
            trimmed = "/" + trimmed;
        }

        if (trimmed.Length > 1 && trimmed.EndsWith('/'))
        {
            trimmed = trimmed.TrimEnd('/');
        }

        return trimmed;
    }
}
