using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kernel.Workers;

/// <summary>
/// Base background worker with standardized logging and error handling.
/// Modules can create workers by inheriting from this class.
/// </summary>
public abstract class BaseWorker(
    IServiceScopeFactory scopeFactory,
    ILogger logger,
    TimeSpan? interval = null) : BackgroundService
{
    protected TimeSpan Interval { get; set; } = interval ?? TimeSpan.FromMinutes(1);
    private bool _cancellationRequested = false;

    protected BaseWorker(IServiceScopeFactory scopeFactory, ILogger logger)
        : this(scopeFactory, logger, null)
    {
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var workerName = GetType().Name;
        logger.LogInformation("Worker {WorkerName} started.", workerName);

        while (!stoppingToken.IsCancellationRequested && !_cancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await ExecuteJobAsync(scope.ServiceProvider, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error in worker {WorkerName}.", workerName);
            }

            await Task.Delay(Interval, stoppingToken);
        }

        logger.LogInformation("Worker {WorkerName} stopped.", workerName);
    }

    /// <summary>
    /// Requests the worker to stop after the current iteration.
    /// </summary>
    protected void RequestCancellation()
    {
        _cancellationRequested = true;
    }

    /// <summary>
    /// Executes the work for each interval.
    /// </summary>
    protected abstract Task ExecuteJobAsync(IServiceProvider services, CancellationToken cancellationToken);
}