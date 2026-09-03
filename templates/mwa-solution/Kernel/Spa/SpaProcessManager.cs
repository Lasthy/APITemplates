using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Sockets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kernel.Spa;

/// <summary>
/// Manages the lifecycle of child frontend development server processes spawned when
/// <see cref="SpaAppOptions.AutoStartDevServer"/> is enabled in Development mode.
/// </summary>
public sealed class SpaProcessManager : IDisposable
{
    private readonly ConcurrentBag<Process> _processes = [];
    private readonly ILogger<SpaProcessManager>? _logger;
    private bool _disposed;

    public SpaProcessManager(ILogger<SpaProcessManager>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Starts the development server for the specified SPA application if <see cref="SpaAppOptions.AutoStartDevServer"/> is enabled.
    /// </summary>
    public async Task StartDevServerAsync(SpaAppOptions app, string contentRootPath, CancellationToken ct = default)
    {
        if (!app.AutoStartDevServer || string.IsNullOrWhiteSpace(app.DevServerCommand))
        {
            return;
        }

        var workingDir = Path.IsPathRooted(app.SourcePath)
            ? app.SourcePath
            : Path.Combine(contentRootPath, app.SourcePath);

        if (!Directory.Exists(workingDir))
        {
            _logger?.LogWarning(
                "[SPA:{AppName}] Cannot auto-start dev server: source directory '{WorkingDir}' does not exist.",
                app.Name, workingDir);
            return;
        }

        // Check if dev server is already listening on target URL (e.g. started in a separate terminal)
        if (!string.IsNullOrWhiteSpace(app.DevServerUrl) && await IsServerListeningAsync(app.DevServerUrl, TimeSpan.FromMilliseconds(500)))
        {
            _logger?.LogInformation(
                "[SPA:{AppName}] Development server is already listening at {DevServerUrl}. Skipping auto-start command.",
                app.Name, app.DevServerUrl);
            return;
        }

        _logger?.LogInformation(
            "[SPA:{AppName}] Auto-starting dev server with command '{Command}' in '{WorkingDir}'...",
            app.Name, app.DevServerCommand, workingDir);

        try
        {
            var isWindows = OperatingSystem.IsWindows();
            var fileName = isWindows ? "cmd.exe" : "sh";
            var args = isWindows ? $"/c {app.DevServerCommand}" : $"-c \"{app.DevServerCommand}\"";

            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };

            process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    _logger?.LogInformation("[SPA:{AppName}] {Message}", app.Name, e.Data);
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    _logger?.LogWarning("[SPA:{AppName}] {Message}", app.Name, e.Data);
                }
            };

            process.Exited += (_, _) =>
            {
                _logger?.LogInformation("[SPA:{AppName}] Dev server process exited.", app.Name);
            };

            if (process.Start())
            {
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                _processes.Add(process);
                _logger?.LogInformation("[SPA:{AppName}] Dev server process started (PID: {Pid}).", app.Name, process.Id);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[SPA:{AppName}] Failed to auto-start dev server command '{Command}'.", app.Name, app.DevServerCommand);
        }
    }

    /// <summary>
    /// Gracefully registers process cleanup with <see cref="IHostApplicationLifetime"/>.
    /// </summary>
    public void RegisterWithLifetime(IHostApplicationLifetime lifetime)
    {
        lifetime.ApplicationStopping.Register(StopAll);
    }

    /// <summary>
    /// Kills all active dev server child processes and their entire process trees.
    /// </summary>
    public void StopAll()
    {
        foreach (var process in _processes)
        {
            try
            {
                if (!process.HasExited)
                {
                    _logger?.LogInformation("Stopping SPA dev server process tree (PID: {Pid})...", process.Id);
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(3000);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Exception while terminating SPA dev server process.");
            }
        }
    }

    private static async Task<bool> IsServerListeningAsync(string url, TimeSpan timeout)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        try
        {
            using var tcp = new TcpClient();
            var connectTask = tcp.ConnectAsync(uri.Host, uri.Port);
            var timeoutTask = Task.Delay(timeout);
            var completed = await Task.WhenAny(connectTask, timeoutTask);
            return completed == connectTask && tcp.Connected;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopAll();
    }
}
