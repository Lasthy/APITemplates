using System.Net;

namespace Kernel.Spa;

/// <summary>
/// HTML templates for developer diagnostics when SPA dev servers are offline or static assets are missing.
/// </summary>
public static class SpaHtmlTemplates
{
    /// <summary>
    /// Generates a modern, dark-themed HTML page informing the developer that the SPA dev server is offline.
    /// </summary>
    public static string GetDevServerOfflineHtml(SpaAppOptions app, string? errorDetails = null)
    {
        var appName = WebUtility.HtmlEncode(app.Name);
        var owner = WebUtility.HtmlEncode(app.Owner);
        var basePath = WebUtility.HtmlEncode(app.BasePath);
        var devUrl = WebUtility.HtmlEncode(app.DevServerUrl ?? "(not configured)");
        var sourcePath = WebUtility.HtmlEncode(app.SourcePath);
        var error = !string.IsNullOrWhiteSpace(errorDetails)
            ? $"<div class=\"error-detail\"><span class=\"label\">Details:</span> {WebUtility.HtmlEncode(errorDetails)}</div>"
            : "";

        return $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="UTF-8">
            <meta name="viewport" content="width=device-width, initial-scale=1.0">
            <title>SPA Dev Server Offline — {{appName}}</title>
            <style>
                :root {
                    --bg-color: #0f172a;
                    --card-bg: #1e293b;
                    --border-color: #334155;
                    --text-primary: #f8fafc;
                    --text-secondary: #94a3b8;
                    --accent-color: #38bdf8;
                    --accent-warn: #fbbf24;
                    --code-bg: #090d16;
                }
                * { box-sizing: border-box; margin: 0; padding: 0; }
                body {
                    background-color: var(--bg-color);
                    color: var(--text-primary);
                    font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif;
                    display: flex;
                    align-items: center;
                    justify-content: center;
                    min-height: 100vh;
                    padding: 1.5rem;
                }
                .container {
                    background: var(--card-bg);
                    border: 1px solid var(--border-color);
                    border-radius: 12px;
                    max-width: 640px;
                    width: 100%;
                    padding: 2rem;
                    box-shadow: 0 20px 25px -5px rgba(0, 0, 0, 0.5), 0 8px 10px -6px rgba(0, 0, 0, 0.4);
                }
                .header {
                    display: flex;
                    align-items: center;
                    gap: 12px;
                    margin-bottom: 1.25rem;
                }
                .badge {
                    background: rgba(251, 191, 36, 0.15);
                    color: var(--accent-warn);
                    border: 1px solid rgba(251, 191, 36, 0.3);
                    font-size: 0.75rem;
                    font-weight: 600;
                    text-transform: uppercase;
                    padding: 4px 8px;
                    border-radius: 6px;
                    letter-spacing: 0.05em;
                }
                h1 {
                    font-size: 1.35rem;
                    font-weight: 700;
                    color: var(--text-primary);
                }
                p {
                    color: var(--text-secondary);
                    font-size: 0.95rem;
                    line-height: 1.5;
                    margin-bottom: 1.25rem;
                }
                .info-grid {
                    display: grid;
                    grid-template-columns: auto 1fr;
                    gap: 8px 16px;
                    background: var(--code-bg);
                    border: 1px solid var(--border-color);
                    border-radius: 8px;
                    padding: 12px 16px;
                    font-size: 0.875rem;
                    margin-bottom: 1.25rem;
                }
                .info-grid .key {
                    color: var(--text-secondary);
                    font-weight: 500;
                }
                .info-grid .val {
                    color: var(--accent-color);
                    font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
                }
                .action-box {
                    background: rgba(56, 189, 248, 0.05);
                    border: 1px solid rgba(56, 189, 248, 0.2);
                    border-radius: 8px;
                    padding: 1rem;
                    margin-bottom: 1.25rem;
                }
                .action-title {
                    font-weight: 600;
                    font-size: 0.875rem;
                    color: var(--accent-color);
                    margin-bottom: 0.5rem;
                }
                pre {
                    background: var(--code-bg);
                    border: 1px solid var(--border-color);
                    border-radius: 6px;
                    padding: 10px 12px;
                    font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
                    font-size: 0.875rem;
                    color: #34d399;
                    overflow-x: auto;
                }
                .error-detail {
                    font-size: 0.8rem;
                    color: #f87171;
                    font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
                    word-break: break-all;
                    margin-top: 0.75rem;
                }
                .footer {
                    font-size: 0.775rem;
                    color: var(--text-secondary);
                    text-align: center;
                    border-top: 1px solid var(--border-color);
                    padding-top: 1rem;
                }
            </style>
        </head>
        <body>
            <div class="container">
                <div class="header">
                    <span class="badge">Offline</span>
                    <h1>SPA Dev Server Unreachable</h1>
                </div>
                <p>
                    ASP.NET Core reverse proxy attempted to forward the request for <strong>{{basePath}}</strong> to the frontend development server, but the target server refused the connection.
                </p>
                <div class="info-grid">
                    <span class="key">SPA Name:</span><span class="val">{{appName}}</span>
                    <span class="key">Owner:</span><span class="val">{{owner}}</span>
                    <span class="key">Base Path:</span><span class="val">{{basePath}}</span>
                    <span class="key">Target Dev URL:</span><span class="val">{{devUrl}}</span>
                </div>
                <div class="action-box">
                    <div class="action-title">Start the development server:</div>
                    <pre><code>cd {{sourcePath}} && npm run dev</code></pre>
                </div>
                {{error}}
                <div class="footer">
                    Modular Web API (MWA) SPA Gateway &bull; Auto-refreshes when server is active
                </div>
            </div>
            <script>
                // Auto-retry every 3 seconds
                setTimeout(() => {
                    fetch(window.location.href, { method: 'HEAD' })
                        .then(res => { if (res.ok) window.location.reload(); })
                        .catch(() => {});
                }, 3000);
            </script>
        </body>
        </html>
        """;
    }

    /// <summary>
    /// Generates an HTML page informing that production compiled assets are missing for this SPA.
    /// </summary>
    public static string GetMissingDistFolderHtml(SpaAppOptions app)
    {
        var appName = WebUtility.HtmlEncode(app.Name);
        var owner = WebUtility.HtmlEncode(app.Owner);
        var basePath = WebUtility.HtmlEncode(app.BasePath);
        var distPath = WebUtility.HtmlEncode(app.DistPath);
        var sourcePath = WebUtility.HtmlEncode(app.SourcePath);

        return $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="UTF-8">
            <meta name="viewport" content="width=device-width, initial-scale=1.0">
            <title>SPA Static Assets Missing — {{appName}}</title>
            <style>
                body { background: #0f172a; color: #f8fafc; font-family: sans-serif; display: flex; align-items: center; justify-content: center; min-height: 100vh; margin: 0; padding: 1.5rem; }
                .container { background: #1e293b; border: 1px solid #334155; border-radius: 12px; max-width: 600px; width: 100%; padding: 2rem; }
                h1 { font-size: 1.25rem; color: #fbbf24; margin-bottom: 1rem; }
                p { color: #94a3b8; font-size: 0.95rem; line-height: 1.5; margin-bottom: 1rem; }
                code { color: #38bdf8; font-family: monospace; }
                pre { background: #090d16; border: 1px solid #334155; border-radius: 6px; padding: 10px; color: #34d399; font-family: monospace; font-size: 0.875rem; }
            </style>
        </head>
        <body>
            <div class="container">
                <h1>SPA Compiled Assets Not Found</h1>
                <p>The SPA <strong>{{appName}}</strong> (Owner: <code>{{owner}}</code>) is mounted at <code>{{basePath}}</code>, but its compiled distribution directory does not exist:</p>
                <pre><code>{{distPath}}</code></pre>
                <p style="margin-top: 1rem;">To compile the production bundle, run:</p>
                <pre><code>cd {{sourcePath}} && npm run build</code></pre>
            </div>
        </body>
        </html>
        """;
    }
}
