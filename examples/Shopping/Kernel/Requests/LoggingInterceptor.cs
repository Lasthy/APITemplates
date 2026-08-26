using System.Diagnostics;
using Kernel.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kernel.Requests;

/// <summary>
/// Applies logging to a request handler execution.
/// Logs request dispatch, payload, duration, and outcome.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public class LoggingAttribute : RequestInterceptorAttribute
{
    /// <summary>
    /// The log level to use for request start and successful completion messages.
    /// Defaults to <see cref="LogLevel.Information"/>.
    /// </summary>
    public LogLevel LogLevel { get; set; } = LogLevel.Information;

    /// <summary>
    /// Whether to log the request payload. Defaults to <c>true</c>.
    /// </summary>
    public bool IncludePayload { get; set; } = true;

    public override IRequestInterceptor CreateInterceptor(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        return new LoggingInterceptor(loggerFactory, LogLevel, IncludePayload);
    }
}

/// <summary>
/// Interceptor that logs request execution, payload, execution time, and outcome.
/// </summary>
public class LoggingInterceptor : IRequestInterceptor
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly LogLevel _logLevel;
    private readonly bool _includePayload;

    public LoggingInterceptor(
        ILoggerFactory loggerFactory,
        LogLevel logLevel = LogLevel.Information,
        bool includePayload = true)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _logLevel = logLevel;
        _includePayload = includePayload;
    }

    public async Task<Result<TResponse>> InterceptAsync<TResponse>(
        IRequest<TResponse> request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken = default)
    {
        var requestType = request.GetType();
        var logger = _loggerFactory.CreateLogger(requestType);
        var requestName = requestType.Name;

        if (_includePayload)
        {
            logger.Log(_logLevel, "Handling request {RequestName}: {@Request}", requestName, request);
        }
        else
        {
            logger.Log(_logLevel, "Handling request {RequestName}", requestName);
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await next();
            stopwatch.Stop();

            if (result.IsSuccess)
            {
                logger.Log(
                    _logLevel,
                    "Handled request {RequestName} successfully in {ElapsedMilliseconds}ms",
                    requestName,
                    stopwatch.ElapsedMilliseconds);
            }
            else
            {
                var errorMessage = result.Error?.Message ?? string.Join("; ", result.Messages);
                logger.LogWarning(
                    result.Error,
                    "Handled request {RequestName} with failure in {ElapsedMilliseconds}ms: {ErrorMessage}",
                    requestName,
                    stopwatch.ElapsedMilliseconds,
                    errorMessage);
            }

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(
                ex,
                "Request {RequestName} failed after {ElapsedMilliseconds}ms due to an unhandled exception: {ErrorMessage}",
                requestName,
                stopwatch.ElapsedMilliseconds,
                ex.Message);

            return Result.Failure<TResponse>(ex);
        }
    }
}
