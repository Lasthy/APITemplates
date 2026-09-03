using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Forwarder;

namespace Kernel.Spa.Extensions;

/// <summary>
/// Extension methods for configuring Single Page Application (SPA) services and pipeline middleware.
/// </summary>
public static class SpaExtensions
{
    /// <summary>
    /// Registers SPA reverse proxy forwarding infrastructure and <see cref="SpaRegistry"/> in DI.
    /// </summary>
    public static IServiceCollection AddSpaServices(this IServiceCollection services)
    {
        var existing = services.LastOrDefault(d => d.ServiceType == typeof(SpaRegistry))?.ImplementationInstance as SpaRegistry;
        if (existing == null)
        {
            services.RemoveAll<SpaRegistry>();
            services.AddSingleton(new SpaRegistry());
        }

        // Register YARP HTTP forwarder for development reverse proxying
        services.AddHttpForwarder();

        // Register pooled HttpMessageInvoker for high-performance HTTP/2 and WebSocket forwarding
        services.TryAddSingleton(sp => new HttpMessageInvoker(new SocketsHttpHandler
        {
            UseProxy = false,
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None,
            EnableMultipleHttp2Connections = true
        }));

        services.TryAddSingleton<SpaProcessManager>();

        return services;
    }

    /// <summary>
    /// Registers a module-owned SPA application into the central <see cref="SpaRegistry"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="moduleName">The owner module name (e.g. "Identity").</param>
    /// <param name="options">The SPA configuration options.</param>
    public static IServiceCollection AddModuleSpa(this IServiceCollection services, string moduleName, SpaAppOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return services.AddModuleSpa(moduleName, options.Name, options);
    }

    /// <summary>
    /// Registers a module-owned SPA application into the central <see cref="SpaRegistry"/> with an explicit name.
    /// </summary>
    public static IServiceCollection AddModuleSpa(this IServiceCollection services, string moduleName, string appName, SpaAppOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        ArgumentException.ThrowIfNullOrWhiteSpace(appName);
        ArgumentNullException.ThrowIfNull(options);

        // Compute full name if not prefixed
        var fullName = appName.StartsWith(moduleName + ".", StringComparison.OrdinalIgnoreCase)
            ? appName
            : $"{moduleName}.{appName}";

        var configured = options with
        {
            Name = fullName,
            Owner = moduleName
        };

        var descriptor = services.LastOrDefault(d => d.ServiceType == typeof(SpaRegistry));
        SpaRegistry registry;
        if (descriptor?.ImplementationInstance is SpaRegistry instance)
        {
            registry = instance;
        }
        else
        {
            registry = new SpaRegistry();
            services.RemoveAll<SpaRegistry>();
            services.AddSingleton(registry);
        }

        registry.RegisterApp(configured);

        return services;
    }

    /// <summary>
    /// Registers multiple module-owned SPA applications into the central <see cref="SpaRegistry"/>.
    /// </summary>
    public static IServiceCollection AddModuleSpas(this IServiceCollection services, string moduleName, IEnumerable<SpaAppOptions> apps)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        ArgumentNullException.ThrowIfNull(apps);

        foreach (var app in apps)
        {
            services.AddModuleSpa(moduleName, app.Name, app);
        }

        return services;
    }

    /// <summary>
    /// Registers module-owned SPA applications defined in a configuration section (e.g. from <c>appsettings.{ModuleName}.json</c> under <c>Spa:Apps</c>).
    /// </summary>
    public static IServiceCollection AddModuleSpas(this IServiceCollection services, string moduleName, IConfigurationSection section)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        ArgumentNullException.ThrowIfNull(section);

        var apps = section.Get<Dictionary<string, SpaAppOptions>>();
        if (apps != null)
        {
            foreach (var (appName, options) in apps)
            {
                services.AddModuleSpa(moduleName, appName, options);
            }
        }

        return services;
    }

    /// <summary>
    /// Configures SPA serving for all registered Host-level and Module-defined SPAs:
    /// In Development, reverse-proxies requests to the respective frontend dev servers (including HMR WebSockets).
    /// In Production, serves compiled static files from distribution directories and falls back to index.html for client-side routing.
    /// Excluded paths (e.g. <c>/api</c>, <c>/openapi</c>, <c>/scalar</c>, <c>/docs</c>, <c>/hubs</c>) are never caught by the SPA.
    /// </summary>
    public static void UseSpa(this WebApplication app)
    {
        var spaSettings = app.Services.GetService<SpaSettings>();
        if (spaSettings == null || !spaSettings.Enabled)
        {
            return;
        }

        var registry = app.Services.GetRequiredService<SpaRegistry>();

        // Register Host-level apps from appsettings.Spa.json
        foreach (var (appName, hostApp) in spaSettings.Apps)
        {
            var resolvedApp = hostApp with
            {
                Name = string.IsNullOrWhiteSpace(hostApp.Name) || hostApp.Name == "Web" ? appName : hostApp.Name,
                Owner = "Host"
            };

            if (!registry.TryGetApp(resolvedApp.Name, out _))
            {
                registry.RegisterApp(resolvedApp);
            }
        }

        // If no SPAs were registered at all, add a default Host Web SPA
        if (registry.Apps.Count == 0)
        {
            registry.RegisterApp(new SpaAppOptions
            {
                Name = "Web",
                Owner = "Host",
                BasePath = "/",
                DevServerUrl = "http://localhost:5173",
                SourcePath = "ClientApp",
                DistPath = "ClientApp/dist"
            });
        }

        var orderedApps = registry.GetOrderedApps();
        var isDev = app.Environment.IsDevelopment();
        var environment = app.Services.GetRequiredService<IWebHostEnvironment>();
        var forwarder = app.Services.GetRequiredService<IHttpForwarder>();
        var httpClient = app.Services.GetRequiredService<HttpMessageInvoker>();
        var loggerFactory = app.Services.GetService<ILoggerFactory>();
        var logger = loggerFactory?.CreateLogger("Kernel.Spa");

        var forwarderConfig = new ForwarderRequestConfig
        {
            ActivityTimeout = TimeSpan.FromSeconds(spaSettings.ProxyTimeoutSeconds)
        };

        // ── Development: Auto-start dev server processes if configured ─────────
        if (isDev)
        {
            var processManager = app.Services.GetService<SpaProcessManager>();
            var lifetime = app.Services.GetService<IHostApplicationLifetime>();
            if (processManager != null && lifetime != null)
            {
                processManager.RegisterWithLifetime(lifetime);
            }

            foreach (var spa in orderedApps.Where(a => a.AutoStartDevServer))
            {
                _ = processManager?.StartDevServerAsync(spa, environment.ContentRootPath);
            }
        }

        // ── Production: Mount static file providers for each SPA ───────────────
        var mountedStaticRoots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!isDev)
        {
            foreach (var spa in orderedApps)
            {
                var resolvedDist = ResolveDistPath(environment, spa.DistPath);
                if (resolvedDist != null && Directory.Exists(resolvedDist))
                {
                    mountedStaticRoots[spa.Name] = resolvedDist;
                    var fileProvider = new PhysicalFileProvider(resolvedDist);

                    if (spa.BasePath == "/")
                    {
                        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = fileProvider });
                        app.UseStaticFiles(new StaticFileOptions { FileProvider = fileProvider });
                    }
                    else
                    {
                        app.UseDefaultFiles(new DefaultFilesOptions
                        {
                            FileProvider = fileProvider,
                            RequestPath = spa.BasePath
                        });
                        app.UseStaticFiles(new StaticFileOptions
                        {
                            FileProvider = fileProvider,
                            RequestPath = spa.BasePath
                        });
                    }

                    logger?.LogInformation(
                        "Mounted production static assets for SPA '{SpaName}' ({Owner}) at '{BasePath}' from '{DistPath}'",
                        spa.Name, spa.Owner, spa.BasePath, resolvedDist);
                }
                else
                {
                    logger?.LogWarning(
                        "SPA '{SpaName}' ({Owner}) dist directory not found at '{DistPath}'. Run 'npm run build' in '{SourcePath}'.",
                        spa.Name, spa.Owner, spa.DistPath, spa.SourcePath);
                }
            }
        }

        // ── Fallback Endpoint (Reverse Proxy in Dev, index.html Fallback in Prod) ────
        app.MapFallback(async (HttpContext context) =>
        {
            var path = context.Request.Path;

            // 1. Never catch excluded prefixes (API controllers, OpenAPI docs, SignalR hubs, health checks)
            if (IsExcluded(path, spaSettings.ExcludedPrefixes))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            // 2. Find matching SPA app based on BasePath length descending
            var matchedSpa = FindMatchingApp(path, registry.GetOrderedApps());
            if (matchedSpa == null)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            // 3. Development Mode: Reverse proxy to dev server
            if (isDev && !string.IsNullOrWhiteSpace(matchedSpa.DevServerUrl))
            {
                var targetPrefix = matchedSpa.DevServerUrl.TrimEnd('/');
                HttpTransformer transformer = matchedSpa.StripBasePathOnProxy && matchedSpa.BasePath != "/"
                    ? new StripPathHttpTransformer(matchedSpa.BasePath)
                    : HttpTransformer.Default;

                var error = await forwarder.SendAsync(
                    context,
                    targetPrefix,
                    httpClient,
                    forwarderConfig,
                    transformer);

                if (error != ForwarderError.None && !context.Response.HasStarted)
                {
                    context.Response.Clear();
                    context.Response.StatusCode = StatusCodes.Status502BadGateway;
                    context.Response.ContentType = "text/html; charset=utf-8";
                    await context.Response.WriteAsync(SpaHtmlTemplates.GetDevServerOfflineHtml(matchedSpa, error.ToString()));
                }
                return;
            }

            // 4. Production Mode: Serve fallback index.html
            // If path has a file extension that wasn't found by static files, return 404 (don't corrupt scripts/styles)
            if (HasFileExtension(path))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            if (mountedStaticRoots.TryGetValue(matchedSpa.Name, out var distRoot))
            {
                var fallbackFilePath = Path.Combine(distRoot, matchedSpa.FallbackFile);
                if (File.Exists(fallbackFilePath))
                {
                    context.Response.ContentType = "text/html; charset=utf-8";
                    await context.Response.SendFileAsync(fallbackFilePath);
                    return;
                }
            }

            // If dist folder or fallback file is missing, render informative diagnostic
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync(SpaHtmlTemplates.GetMissingDistFolderHtml(matchedSpa));
        });
    }

    private static bool IsExcluded(PathString path, IEnumerable<string> excludedPrefixes)
    {
        if (!path.HasValue) return false;

        foreach (var prefix in excludedPrefixes)
        {
            if (string.IsNullOrWhiteSpace(prefix)) continue;
            if (path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static SpaAppOptions? FindMatchingApp(PathString path, IReadOnlyList<SpaAppOptions> orderedApps)
    {
        var pathValue = path.Value ?? "/";

        foreach (var app in orderedApps)
        {
            if (app.BasePath == "/")
            {
                return app;
            }

            if (pathValue.Equals(app.BasePath, StringComparison.OrdinalIgnoreCase) ||
                path.StartsWithSegments(app.BasePath, StringComparison.OrdinalIgnoreCase))
            {
                return app;
            }
        }

        return null;
    }

    private static bool HasFileExtension(PathString path)
    {
        var fileName = Path.GetFileName(path.Value);
        return !string.IsNullOrEmpty(fileName) && fileName.Contains('.');
    }

    private static string? ResolveDistPath(IWebHostEnvironment environment, string distPath)
    {
        if (Path.IsPathRooted(distPath))
        {
            return Directory.Exists(distPath) ? distPath : null;
        }

        // 1. Check relative to ContentRootPath
        var contentPath = Path.Combine(environment.ContentRootPath, distPath);
        if (Directory.Exists(contentPath))
        {
            return contentPath;
        }

        // 2. Check relative to AppContext.BaseDirectory (build/publish output)
        var basePath = Path.Combine(AppContext.BaseDirectory, distPath);
        if (Directory.Exists(basePath))
        {
            return basePath;
        }

        // 3. Fallback to WebRootPath (wwwroot)
        if (!string.IsNullOrWhiteSpace(environment.WebRootPath) && Directory.Exists(environment.WebRootPath))
        {
            return environment.WebRootPath;
        }

        return null;
    }

    private static T? GetRegisteredSingleton<T>(this IServiceCollection services) where T : class
    {
        var descriptor = services.LastOrDefault(d => d.ServiceType == typeof(T));
        return descriptor?.ImplementationInstance as T;
    }

    private sealed class StripPathHttpTransformer(string basePath) : HttpTransformer
    {
        public override async ValueTask TransformRequestAsync(
            HttpContext httpContext,
            HttpRequestMessage proxyRequest,
            string destinationPrefix,
            CancellationToken cancellationToken)
        {
            await base.TransformRequestAsync(httpContext, proxyRequest, destinationPrefix, cancellationToken);

            var originalPath = httpContext.Request.Path.Value ?? "";
            if (originalPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
            {
                var stripped = originalPath.Substring(basePath.Length);
                if (!stripped.StartsWith('/'))
                {
                    stripped = "/" + stripped;
                }
                var targetUri = new Uri(destinationPrefix.TrimEnd('/') + stripped + httpContext.Request.QueryString);
                proxyRequest.RequestUri = targetUri;
            }
        }
    }
}
