using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Kernel.Json;
using Webhooks.Configuration;
using Webhooks.Contracts.Constants;
using Webhooks.Entities;

namespace Webhooks.Delivery;

/// <summary>
/// HttpClient-based delivery engine that formats, signs, and dispatches webhook payloads.
/// </summary>
public class WebhookDeliveryEngine : IWebhookDeliveryEngine
{
    private readonly HttpClient _httpClient;
    private readonly ISignatureService _signatureService;
    private readonly IJsonSerializer _jsonSerializer;
    private readonly WebhookSettings _settings;

    public WebhookDeliveryEngine(
        HttpClient httpClient,
        ISignatureService signatureService,
        IJsonSerializer jsonSerializer,
        WebhookSettings settings)
    {
        _httpClient = httpClient;
        _signatureService = signatureService;
        _jsonSerializer = jsonSerializer;
        _settings = settings;
    }

    public async Task<WebhookDelivery> DeliverAsync(
        WebhookSubscription subscription,
        WebhookEvent webhookEvent,
        int attemptNumber,
        CancellationToken ct = default)
    {
        var deliveryId = DefaultId.NewUlid();
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

        // 1. Build envelope payload
        object? dataPayload;
        try
        {
            dataPayload = _jsonSerializer.Deserialize<JsonElement>(webhookEvent.Payload);
        }
        catch
        {
            dataPayload = webhookEvent.Payload;
        }

        var envelope = new
        {
            id = deliveryId.ToString(),
            @event = webhookEvent.EventType,
            timestamp = DateTime.UtcNow.ToString("o"),
            data = dataPayload
        };

        var requestBody = _jsonSerializer.Serialize(envelope);

        // 2. Sign payload
        var signature = _signatureService.Sign(subscription.Secret, timestamp, requestBody);

        // 3. Prepare HTTP request
        using var request = new HttpRequestMessage(HttpMethod.Post, subscription.Url);
        request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

        request.Headers.TryAddWithoutValidation(WebhookHeaders.WebhookId, deliveryId.ToString());
        request.Headers.TryAddWithoutValidation(WebhookHeaders.WebhookEvent, webhookEvent.EventType);
        request.Headers.TryAddWithoutValidation(WebhookHeaders.WebhookTimestamp, timestamp);
        request.Headers.TryAddWithoutValidation(WebhookHeaders.WebhookSignature, signature);

        // 4. Custom headers
        if (!string.IsNullOrWhiteSpace(subscription.CustomHeadersJson))
        {
            try
            {
                var customHeaders = _jsonSerializer.Deserialize<Dictionary<string, string>>(subscription.CustomHeadersJson);
                if (customHeaders != null)
                {
                    foreach (var (headerName, headerValue) in customHeaders)
                    {
                        if (!request.Headers.TryAddWithoutValidation(headerName, headerValue))
                        {
                            request.Content.Headers.TryAddWithoutValidation(headerName, headerValue);
                        }
                    }
                }
            }
            catch
            {
                // Ignore malformed custom headers JSON
            }
        }

        // 5. Execute request with timeout
        var stopwatch = Stopwatch.StartNew();
        int? statusCode = null;
        string? responseBody = null;
        string? error = null;
        var status = DeliveryStatus.Pending;

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_settings.DeliveryTimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        try
        {
            var response = await _httpClient.SendAsync(request, linkedCts.Token);
            stopwatch.Stop();
            statusCode = (int)response.StatusCode;

            var rawBody = await response.Content.ReadAsStringAsync(linkedCts.Token);
            if (!string.IsNullOrEmpty(rawBody))
            {
                var maxBytes = _settings.MaxResponseBodyBytes > 0 ? _settings.MaxResponseBodyBytes : 4096;
                responseBody = rawBody.Length > maxBytes ? rawBody[..maxBytes] : rawBody;
            }

            if (response.IsSuccessStatusCode)
            {
                status = DeliveryStatus.Succeeded;
            }
            else
            {
                status = DeliveryStatus.Failed;
                error = $"Receiver returned HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
            }
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            stopwatch.Stop();
            status = DeliveryStatus.Failed;
            error = $"Delivery timed out after {_settings.DeliveryTimeoutSeconds} seconds.";
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            status = DeliveryStatus.Failed;
            error = ex.Message;
        }

        return new WebhookDelivery
        {
            Id = deliveryId,
            SubscriptionId = subscription.Id,
            WebhookEventId = webhookEvent.Id,
            EventType = webhookEvent.EventType,
            Status = status,
            ResponseStatusCode = statusCode,
            ResponseBody = responseBody,
            RequestBody = requestBody,
            Error = error,
            AttemptNumber = attemptNumber,
            DurationMs = stopwatch.ElapsedMilliseconds,
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            NextRetryAt = null,
            Subscription = subscription
        };
    }
}
