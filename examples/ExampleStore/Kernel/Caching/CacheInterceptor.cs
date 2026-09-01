using System.Reflection;
using System.Text.RegularExpressions;
using Kernel.Common;
using Kernel.Context;
using Kernel.Requests;
using Microsoft.Extensions.Logging;

namespace Kernel.Caching;

/// <summary>
/// Interceptor that handles response caching for request handlers decorated with <see cref="CacheAttribute"/>.
/// Short-circuits execution on cache hits and automatically persists successful results to cache.
/// </summary>
public class CacheInterceptor : IRequestInterceptor
{
    private static readonly Regex PlaceholderRegex = new(@"\{(?<expr>[^}]+)\}", RegexOptions.Compiled);

    private readonly ICacheService _cacheService;
    private readonly CacheSettings _settings;
    private readonly IScopeContext? _scopeContext;
    private readonly ILogger<CacheInterceptor>? _logger;
    private readonly int _durationSeconds;
    private readonly string? _keyTemplate;
    private readonly string? _keyPrefix;
    private readonly bool _varyByUser;

    public CacheInterceptor(
        ICacheService cacheService,
        CacheSettings settings,
        IScopeContext? scopeContext = null,
        ILogger<CacheInterceptor>? logger = null,
        int durationSeconds = 0,
        string? keyTemplate = null,
        string? keyPrefix = null,
        bool varyByUser = false)
    {
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _scopeContext = scopeContext;
        _logger = logger;
        _durationSeconds = durationSeconds;
        _keyTemplate = keyTemplate;
        _keyPrefix = keyPrefix;
        _varyByUser = varyByUser;
    }

    public async Task<Result<TResponse>> InterceptAsync<TResponse>(
        IRequest<TResponse> request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_settings.Enabled)
        {
            return await next();
        }

        var cacheKey = BuildCacheKey(request);

        var cachedValue = await _cacheService.GetAsync<TResponse>(cacheKey, cancellationToken);
        if (cachedValue != null)
        {
            _logger?.LogDebug("Cache HIT for request {RequestType} with key {CacheKey}", request.GetType().Name, cacheKey);
            return Result.Success(cachedValue);
        }

        _logger?.LogDebug("Cache MISS for request {RequestType} with key {CacheKey}", request.GetType().Name, cacheKey);
        var result = await next();

        if (result.IsSuccess && result.Value != null)
        {
            var expiry = _durationSeconds > 0
                ? TimeSpan.FromSeconds(_durationSeconds)
                : TimeSpan.FromSeconds(_settings.DefaultExpirationSeconds);

            await _cacheService.SetAsync(cacheKey, result.Value, expiry, cancellationToken);
            _logger?.LogDebug("Cached response for request {RequestType} with key {CacheKey} (TTL: {Expiry}s)", request.GetType().Name, cacheKey, expiry.TotalSeconds);
        }

        return result;
    }

    private string BuildCacheKey(object request)
    {
        string key;

        if (!string.IsNullOrWhiteSpace(_keyTemplate))
        {
            key = PlaceholderRegex.Replace(_keyTemplate, match =>
            {
                var expr = match.Groups["expr"].Value.Trim();

                if (expr.Equals("userId", StringComparison.OrdinalIgnoreCase))
                {
                    return _scopeContext?.UserId?.ToString() ?? "anonymous";
                }

                if (expr.Equals("correlationId", StringComparison.OrdinalIgnoreCase))
                {
                    return _scopeContext?.CorrelationId ?? string.Empty;
                }

                var propPath = expr.StartsWith("request.", StringComparison.OrdinalIgnoreCase)
                    ? expr["request.".Length..]
                    : expr;

                var value = ResolvePropertyValue(request, propPath);
                return value?.ToString() ?? "null";
            });
        }
        else
        {
            var requestType = request.GetType();
            var fingerprint = BuildDefaultFingerprint(request);
            key = string.IsNullOrEmpty(fingerprint)
                ? requestType.FullName ?? requestType.Name
                : $"{requestType.FullName ?? requestType.Name}:{fingerprint}";
        }

        if (_varyByUser && (_keyTemplate == null || !_keyTemplate.Contains("{userId}", StringComparison.OrdinalIgnoreCase)))
        {
            var userId = _scopeContext?.UserId?.ToString() ?? "anonymous";
            key = $"u:{userId}:{key}";
        }

        if (!string.IsNullOrWhiteSpace(_keyPrefix))
        {
            key = $"{_keyPrefix}:{key}";
        }

        return key;
    }

    private static object? ResolvePropertyValue(object target, string propertyPath)
    {
        if (target == null || string.IsNullOrWhiteSpace(propertyPath))
            return null;

        var current = target;
        var parts = propertyPath.Split('.');

        foreach (var part in parts)
        {
            if (current == null) return null;

            var type = current.GetType();
            var prop = type.GetProperty(part, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop != null)
            {
                current = prop.GetValue(current);
                continue;
            }

            var field = type.GetField(part, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (field != null)
            {
                current = field.GetValue(current);
                continue;
            }

            return null;
        }

        return current;
    }

    private static string BuildDefaultFingerprint(object request)
    {
        var properties = request.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead)
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (properties.Count == 0)
            return string.Empty;

        var parts = properties.Select(p =>
        {
            var val = p.GetValue(request);
            return $"{p.Name}={val?.ToString() ?? "null"}";
        });

        return string.Join(";", parts);
    }
}
