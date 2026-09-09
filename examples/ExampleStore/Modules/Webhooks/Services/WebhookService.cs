using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Kernel.Common;
using Kernel.Events;
using Kernel.Json;
using Microsoft.EntityFrameworkCore;
using Webhooks.Configuration;
using Webhooks.Contracts.Constants;
using Webhooks.Contracts.Dtos;
using Webhooks.Contracts.Events;
using Webhooks.Delivery;
using Webhooks.Entities;
using Webhooks.Persistence;

namespace Webhooks.Services;

/// <summary>
/// Core implementation of webhook management, SSRF protection, secret rotation, and deliveries.
/// </summary>
public class WebhookService : IWebhookService
{
    private readonly WebhookModuleDbContext _dbContext;
    private readonly IWebhookDeliveryEngine _deliveryEngine;
    private readonly IEventBus _eventBus;
    private readonly IJsonSerializer _jsonSerializer;
    private readonly WebhookSettings _settings;

    public WebhookService(
        WebhookModuleDbContext dbContext,
        IWebhookDeliveryEngine deliveryEngine,
        IEventBus eventBus,
        IJsonSerializer jsonSerializer,
        WebhookSettings settings)
    {
        _dbContext = dbContext;
        _deliveryEngine = deliveryEngine;
        _eventBus = eventBus;
        _jsonSerializer = jsonSerializer;
        _settings = settings;
    }

    public async Task<Result<WebhookSubscriptionDetailDto>> CreateSubscriptionAsync(
        CreateSubscriptionRequest request,
        DefaultId ownerUserId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result.Failure<WebhookSubscriptionDetailDto>(new ArgumentException("Subscription name is required."));
        }

        var urlValidation = ValidateUrl(request.Url);
        if (urlValidation.IsFailure)
        {
            return Result.Failure<WebhookSubscriptionDetailDto>(urlValidation.Error!);
        }

        // Quota check
        var userSubscriptionCount = await _dbContext.Subscriptions
            .CountAsync(s => s.OwnerUserId == ownerUserId && s.DeletedAt == null, ct);

        if (userSubscriptionCount >= _settings.MaxSubscriptionsPerUser)
        {
            return Result.Failure<WebhookSubscriptionDetailDto>(
                new InvalidOperationException($"User has reached the limit of {_settings.MaxSubscriptionsPerUser} active subscriptions."));
        }

        // Generate cryptographically secure secret
        var secret = GenerateSecret();

        var subscription = new WebhookSubscription
        {
            Id = DefaultId.NewUlid(),
            Name = request.Name.Trim(),
            Url = request.Url.Trim(),
            Secret = secret,
            EventTypes = string.IsNullOrWhiteSpace(request.EventTypes) ? null : request.EventTypes.Trim(),
            Description = request.Description?.Trim(),
            CustomHeadersJson = request.CustomHeadersJson,
            OwnerUserId = ownerUserId,
            Status = "Active",
            ConsecutiveFailures = 0,
            TotalDeliveries = 0,
            TotalFailures = 0,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Subscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync(ct);

        await _eventBus.PublishAsync(
            new WebhookSubscriptionCreatedEvent(subscription.Id, subscription.Url, subscription.EventTypes), ct);

        return Result.Success(MapToDetailDto(subscription, secret));
    }

    public async Task<Result<IReadOnlyList<WebhookSubscriptionDto>>> GetSubscriptionsAsync(
        DefaultId? currentUserId,
        bool isSystem,
        CancellationToken ct = default)
    {
        var query = _dbContext.Subscriptions.AsNoTracking();

        if (!isSystem)
        {
            if (!currentUserId.HasValue)
            {
                return Result.Failure<IReadOnlyList<WebhookSubscriptionDto>>(new UnauthorizedAccessException("User context required."));
            }
            query = query.Where(s => s.OwnerUserId == currentUserId.Value);
        }

        var items = await query
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new WebhookSubscriptionDto(
                s.Id,
                s.Name,
                s.Url,
                s.Status,
                s.EventTypes,
                s.CreatedAt))
            .ToListAsync(ct);

        return Result.Success<IReadOnlyList<WebhookSubscriptionDto>>(items);
    }

    public async Task<Result<WebhookSubscriptionDetailDto>> GetSubscriptionAsync(
        DefaultId subscriptionId,
        DefaultId? currentUserId,
        bool isSystem,
        CancellationToken ct = default)
    {
        var subscription = await _dbContext.Subscriptions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == subscriptionId, ct);

        if (subscription == null)
        {
            return Result.Failure<WebhookSubscriptionDetailDto>(new KeyNotFoundException($"Webhook subscription '{subscriptionId}' not found."));
        }

        if (!isSystem && subscription.OwnerUserId != currentUserId)
        {
            return Result.Failure<WebhookSubscriptionDetailDto>(new UnauthorizedAccessException("Access denied."));
        }

        return Result.Success(MapToDetailDto(subscription));
    }

    public async Task<Result<WebhookSubscriptionDetailDto>> UpdateSubscriptionAsync(
        DefaultId subscriptionId,
        UpdateSubscriptionRequest request,
        DefaultId? currentUserId,
        bool isSystem,
        CancellationToken ct = default)
    {
        var subscription = await _dbContext.Subscriptions
            .FirstOrDefaultAsync(s => s.Id == subscriptionId, ct);

        if (subscription == null)
        {
            return Result.Failure<WebhookSubscriptionDetailDto>(new KeyNotFoundException($"Webhook subscription '{subscriptionId}' not found."));
        }

        if (!isSystem && subscription.OwnerUserId != currentUserId)
        {
            return Result.Failure<WebhookSubscriptionDetailDto>(new UnauthorizedAccessException("Access denied."));
        }

        if (!string.IsNullOrWhiteSpace(request.Url) && !string.Equals(subscription.Url, request.Url, StringComparison.OrdinalIgnoreCase))
        {
            var urlValidation = ValidateUrl(request.Url);
            if (urlValidation.IsFailure)
            {
                return Result.Failure<WebhookSubscriptionDetailDto>(urlValidation.Error!);
            }
            subscription.Url = request.Url.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            subscription.Name = request.Name.Trim();
        }

        if (request.EventTypes != null)
        {
            subscription.EventTypes = string.IsNullOrWhiteSpace(request.EventTypes) ? null : request.EventTypes.Trim();
        }

        if (request.Description != null)
        {
            subscription.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        }

        if (request.CustomHeadersJson != null)
        {
            subscription.CustomHeadersJson = string.IsNullOrWhiteSpace(request.CustomHeadersJson) ? null : request.CustomHeadersJson;
        }

        subscription.UpdatedAt = DateTime.UtcNow;
        subscription.UpdatedBy = currentUserId;

        await _dbContext.SaveChangesAsync(ct);

        return Result.Success(MapToDetailDto(subscription));
    }

    public async Task<Result> DeleteSubscriptionAsync(
        DefaultId subscriptionId,
        DefaultId? currentUserId,
        bool isSystem,
        CancellationToken ct = default)
    {
        var subscription = await _dbContext.Subscriptions
            .FirstOrDefaultAsync(s => s.Id == subscriptionId, ct);

        if (subscription == null)
        {
            return Result.Failure(new KeyNotFoundException($"Webhook subscription '{subscriptionId}' not found."));
        }

        if (!isSystem && subscription.OwnerUserId != currentUserId)
        {
            return Result.Failure(new UnauthorizedAccessException("Access denied."));
        }

        subscription.DeletedAt = DateTime.UtcNow;
        subscription.DeletedBy = currentUserId;

        await _dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> PauseSubscriptionAsync(
        DefaultId subscriptionId,
        DefaultId? currentUserId,
        bool isSystem,
        CancellationToken ct = default)
    {
        var subscription = await _dbContext.Subscriptions
            .FirstOrDefaultAsync(s => s.Id == subscriptionId, ct);

        if (subscription == null)
        {
            return Result.Failure(new KeyNotFoundException($"Webhook subscription '{subscriptionId}' not found."));
        }

        if (!isSystem && subscription.OwnerUserId != currentUserId)
        {
            return Result.Failure(new UnauthorizedAccessException("Access denied."));
        }

        subscription.Status = "Paused";
        subscription.UpdatedAt = DateTime.UtcNow;
        subscription.UpdatedBy = currentUserId;

        await _dbContext.SaveChangesAsync(ct);
        await _eventBus.PublishAsync(new WebhookSubscriptionPausedEvent(subscription.Id, "Paused by user"), ct);

        return Result.Success();
    }

    public async Task<Result> ResumeSubscriptionAsync(
        DefaultId subscriptionId,
        DefaultId? currentUserId,
        bool isSystem,
        CancellationToken ct = default)
    {
        var subscription = await _dbContext.Subscriptions
            .FirstOrDefaultAsync(s => s.Id == subscriptionId, ct);

        if (subscription == null)
        {
            return Result.Failure(new KeyNotFoundException($"Webhook subscription '{subscriptionId}' not found."));
        }

        if (!isSystem && subscription.OwnerUserId != currentUserId)
        {
            return Result.Failure(new UnauthorizedAccessException("Access denied."));
        }

        subscription.Status = "Active";
        subscription.ConsecutiveFailures = 0;
        subscription.UpdatedAt = DateTime.UtcNow;
        subscription.UpdatedBy = currentUserId;

        await _dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<string>> RotateSecretAsync(
        DefaultId subscriptionId,
        DefaultId? currentUserId,
        bool isSystem,
        CancellationToken ct = default)
    {
        var subscription = await _dbContext.Subscriptions
            .FirstOrDefaultAsync(s => s.Id == subscriptionId, ct);

        if (subscription == null)
        {
            return Result.Failure<string>(new KeyNotFoundException($"Webhook subscription '{subscriptionId}' not found."));
        }

        if (!isSystem && subscription.OwnerUserId != currentUserId)
        {
            return Result.Failure<string>(new UnauthorizedAccessException("Access denied."));
        }

        var newSecret = GenerateSecret();
        subscription.Secret = newSecret;
        subscription.UpdatedAt = DateTime.UtcNow;
        subscription.UpdatedBy = currentUserId;

        await _dbContext.SaveChangesAsync(ct);
        return Result.Success<string>(newSecret);
    }

    public async Task<Result<WebhookDeliveryDetailDto>> TestSubscriptionAsync(
        DefaultId subscriptionId,
        DefaultId? currentUserId,
        bool isSystem,
        CancellationToken ct = default)
    {
        var subscription = await _dbContext.Subscriptions
            .FirstOrDefaultAsync(s => s.Id == subscriptionId, ct);

        if (subscription == null)
        {
            return Result.Failure<WebhookDeliveryDetailDto>(new KeyNotFoundException($"Webhook subscription '{subscriptionId}' not found."));
        }

        if (!isSystem && subscription.OwnerUserId != currentUserId)
        {
            return Result.Failure<WebhookDeliveryDetailDto>(new UnauthorizedAccessException("Access denied."));
        }

        var testEvent = new WebhookTestEvent(subscriptionId, "Webhook connectivity test", DateTime.UtcNow);
        var webhookEvent = new WebhookEvent
        {
            Id = DefaultId.NewUlid(),
            EventType = nameof(WebhookTestEvent),
            Payload = _jsonSerializer.Serialize(testEvent),
            IsProcessed = true,
            OccurredAt = DateTime.UtcNow,
            ProcessedAt = DateTime.UtcNow
        };

        var delivery = await _deliveryEngine.DeliverAsync(subscription, webhookEvent, attemptNumber: 1, ct);

        _dbContext.Events.Add(webhookEvent);
        _dbContext.Deliveries.Add(delivery);
        await _dbContext.SaveChangesAsync(ct);

        return Result.Success(MapToDeliveryDetailDto(delivery));
    }

    public async Task<Result<IReadOnlyList<WebhookDeliveryDto>>> GetDeliveriesAsync(
        DeliveriesFilter filter,
        DefaultId? currentUserId,
        bool isSystem,
        CancellationToken ct = default)
    {
        var query = _dbContext.Deliveries.AsNoTracking().Include(d => d.Subscription).AsQueryable();

        if (!isSystem)
        {
            if (!currentUserId.HasValue)
            {
                return Result.Failure<IReadOnlyList<WebhookDeliveryDto>>(new UnauthorizedAccessException("User context required."));
            }
            query = query.Where(d => d.Subscription.OwnerUserId == currentUserId.Value);
        }

        if (filter.SubscriptionId.HasValue)
        {
            query = query.Where(d => d.SubscriptionId == filter.SubscriptionId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            query = query.Where(d => d.Status == filter.Status);
        }

        if (filter.FromDate.HasValue)
        {
            query = query.Where(d => d.CreatedAt >= filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            query = query.Where(d => d.CreatedAt <= filter.ToDate.Value);
        }

        var pageSize = Math.Clamp(filter.PageSize, 1, 100);
        var page = Math.Max(filter.Page, 1);

        var items = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new WebhookDeliveryDto(
                d.Id,
                d.SubscriptionId,
                d.EventType,
                d.Status,
                d.ResponseStatusCode,
                d.AttemptNumber,
                d.DurationMs,
                d.CreatedAt))
            .ToListAsync(ct);

        return Result.Success<IReadOnlyList<WebhookDeliveryDto>>(items);
    }

    public async Task<Result<WebhookDeliveryDetailDto>> GetDeliveryAsync(
        DefaultId deliveryId,
        DefaultId? currentUserId,
        bool isSystem,
        CancellationToken ct = default)
    {
        var delivery = await _dbContext.Deliveries
            .AsNoTracking()
            .Include(d => d.Subscription)
            .FirstOrDefaultAsync(d => d.Id == deliveryId, ct);

        if (delivery == null)
        {
            return Result.Failure<WebhookDeliveryDetailDto>(new KeyNotFoundException($"Delivery '{deliveryId}' not found."));
        }

        if (!isSystem && delivery.Subscription.OwnerUserId != currentUserId)
        {
            return Result.Failure<WebhookDeliveryDetailDto>(new UnauthorizedAccessException("Access denied."));
        }

        return Result.Success(MapToDeliveryDetailDto(delivery));
    }

    public async Task<Result<WebhookDeliveryDetailDto>> ReplayDeliveryAsync(
        DefaultId deliveryId,
        DefaultId? currentUserId,
        bool isSystem,
        CancellationToken ct = default)
    {
        var delivery = await _dbContext.Deliveries
            .Include(d => d.Subscription)
            .FirstOrDefaultAsync(d => d.Id == deliveryId, ct);

        if (delivery == null)
        {
            return Result.Failure<WebhookDeliveryDetailDto>(new KeyNotFoundException($"Delivery '{deliveryId}' not found."));
        }

        if (!isSystem && delivery.Subscription.OwnerUserId != currentUserId)
        {
            return Result.Failure<WebhookDeliveryDetailDto>(new UnauthorizedAccessException("Access denied."));
        }

        var webhookEvent = await _dbContext.Events.FirstOrDefaultAsync(e => e.Id == delivery.WebhookEventId, ct);
        if (webhookEvent == null)
        {
            // Synthesize event from delivery request body
            webhookEvent = new WebhookEvent
            {
                Id = delivery.WebhookEventId,
                EventType = delivery.EventType,
                Payload = delivery.RequestBody,
                IsProcessed = true,
                OccurredAt = delivery.CreatedAt,
                ProcessedAt = DateTime.UtcNow
            };
        }

        var replayDelivery = await _deliveryEngine.DeliverAsync(
            delivery.Subscription,
            webhookEvent,
            attemptNumber: delivery.AttemptNumber + 1,
            ct);

        _dbContext.Deliveries.Add(replayDelivery);
        await _dbContext.SaveChangesAsync(ct);

        return Result.Success(MapToDeliveryDetailDto(replayDelivery));
    }

    public async Task<Result<DeliveryStatsDto>> GetDeliveryStatsAsync(
        DefaultId? currentUserId,
        bool isSystem,
        CancellationToken ct = default)
    {
        var query = _dbContext.Deliveries.AsNoTracking().Include(d => d.Subscription).AsQueryable();

        if (!isSystem)
        {
            if (!currentUserId.HasValue)
            {
                return Result.Failure<DeliveryStatsDto>(new UnauthorizedAccessException("User context required."));
            }
            query = query.Where(d => d.Subscription.OwnerUserId == currentUserId.Value);
        }

        var total = await query.CountAsync(ct);
        var succeeded = await query.CountAsync(d => d.Status == DeliveryStatus.Succeeded, ct);
        var failed = await query.CountAsync(d => d.Status == DeliveryStatus.Failed, ct);
        var deadLettered = await query.CountAsync(d => d.Status == DeliveryStatus.DeadLettered, ct);
        var pending = await query.CountAsync(d => d.Status == DeliveryStatus.Pending, ct);
        var avgDuration = total > 0
            ? await query.Where(d => d.DurationMs.HasValue).AverageAsync(d => (double?)d.DurationMs, ct) ?? 0.0
            : 0.0;

        return Result.Success(new DeliveryStatsDto(
            total,
            succeeded,
            failed,
            deadLettered,
            pending,
            Math.Round(avgDuration, 2)));
    }

    // ── Helper Methods ────────────────────────────────────────────────────────

    private Result ValidateUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return Result.Failure(new ArgumentException("A valid absolute URL is required."));
        }

        // HTTPS enforcement
        if (_settings.RequireHttps && uri.Scheme != Uri.UriSchemeHttps)
        {
            return Result.Failure(new ArgumentException("Subscription URL must use HTTPS."));
        }

        var host = uri.Host;

        // SSRF protection: Check AllowedUrlPatterns first if defined
        if (_settings.AllowedUrlPatterns is { Length: > 0 } allowed && allowed.Length > 0)
        {
            var isAllowed = allowed.Any(p => MatchesPattern(host, p));
            if (!isAllowed)
            {
                return Result.Failure(new ArgumentException("Subscription URL host is not in the allowed URL list."));
            }
        }

        // SSRF protection: Check BlockedUrlPatterns
        if (_settings.BlockedUrlPatterns is { Length: > 0 } blocked)
        {
            foreach (var pattern in blocked)
            {
                if (MatchesPattern(host, pattern))
                {
                    return Result.Failure(new ArgumentException($"Subscription URL host '{host}' is blocked for security reasons."));
                }
            }
        }

        // Check Loopback / Private IP if host is an IP address
        if (IPAddress.TryParse(host, out var ip))
        {
            if (IPAddress.IsLoopback(ip) || IsPrivateIp(ip))
            {
                return Result.Failure(new ArgumentException($"Subscription URL cannot target loopback or private IP address '{host}'."));
            }
        }

        return Result.Success();
    }

    private static bool MatchesPattern(string host, string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern)) return false;
        pattern = pattern.Trim();

        if (pattern == "*") return true;

        if (pattern.StartsWith("*."))
        {
            var domain = pattern[2..];
            return host.EndsWith(domain, StringComparison.OrdinalIgnoreCase) ||
                   host.Equals(domain, StringComparison.OrdinalIgnoreCase);
        }

        if (pattern.EndsWith(".*"))
        {
            var prefix = pattern[..^1];
            return host.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(host, pattern, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPrivateIp(IPAddress ip)
    {
        var bytes = ip.GetAddressBytes();
        return bytes[0] switch
        {
            10 => true, // 10.0.0.0/8
            172 => bytes[1] >= 16 && bytes[1] <= 31, // 172.16.0.0/12
            192 => bytes[1] == 168, // 192.168.0.0/16
            127 => true, // 127.0.0.0/8
            169 => bytes[1] == 254, // 169.254.0.0/16 (Link Local)
            _ => false
        };
    }

    private static string GenerateSecret()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
    }

    private static WebhookSubscriptionDetailDto MapToDetailDto(WebhookSubscription s, string? plainTextSecret = null)
    {
        return new WebhookSubscriptionDetailDto(
            s.Id,
            s.Name,
            s.Url,
            s.Status,
            s.EventTypes,
            s.Description,
            s.TotalDeliveries,
            s.TotalFailures,
            s.ConsecutiveFailures,
            s.LastDeliveryAt,
            s.CreatedAt,
            plainTextSecret);
    }

    private static WebhookDeliveryDetailDto MapToDeliveryDetailDto(WebhookDelivery d)
    {
        return new WebhookDeliveryDetailDto(
            d.Id,
            d.SubscriptionId,
            d.EventType,
            d.Status,
            d.ResponseStatusCode,
            d.ResponseBody,
            d.RequestBody,
            d.Error,
            d.AttemptNumber,
            d.DurationMs,
            d.CreatedAt,
            d.CompletedAt,
            d.NextRetryAt);
    }
}
