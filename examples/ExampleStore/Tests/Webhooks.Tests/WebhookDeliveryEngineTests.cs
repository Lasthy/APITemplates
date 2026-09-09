using System.Net;
using System.Text.Json;
using Kernel.Json;
using Webhooks.Configuration;
using Webhooks.Contracts.Constants;
using Webhooks.Delivery;
using Webhooks.Entities;
using Xunit;

namespace Webhooks.Tests;

public class WebhookDeliveryEngineTests
{
    private class TestHttpMessageHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }
        public HttpResponseMessage ResponseToReturn { get; set; } = new(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"received\":true}")
        };
        public Exception? ExceptionToThrow { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content != null)
            {
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            if (ExceptionToThrow != null)
            {
                throw ExceptionToThrow;
            }

            return ResponseToReturn;
        }
    }

    private readonly TestHttpMessageHandler _handler = new();
    private readonly HmacSignatureService _signer = new();
    private readonly Kernel.Json.JsonSerializer _jsonSerializer = new();
    private readonly WebhookSettings _settings;
    private readonly WebhookDeliveryEngine _engine;

    public WebhookDeliveryEngineTests()
    {
        _settings = new WebhookSettings(_jsonSerializer);
        var httpClient = new HttpClient(_handler);
        _engine = new WebhookDeliveryEngine(httpClient, _signer, _jsonSerializer, _settings);
    }

    [Fact]
    public async Task DeliverAsync_ConstructsValidEnvelopeAndHeaders()
    {
        var subscription = new WebhookSubscription
        {
            Id = DefaultId.NewUlid(),
            Name = "Order Webhook",
            Url = "https://receiver.example.com/events",
            Secret = "secret-12345",
            CustomHeadersJson = "{\"Authorization\":\"Bearer test-token\",\"X-Custom\":\"Value\"}"
        };

        var webhookEvent = new WebhookEvent
        {
            Id = DefaultId.NewUlid(),
            EventType = "OrderCompletedEvent",
            Payload = "{\"OrderId\":\"01JRXYZ\",\"Amount\":99.50}"
        };

        var delivery = await _engine.DeliverAsync(subscription, webhookEvent, attemptNumber: 1);

        Assert.NotNull(delivery);
        Assert.Equal(DeliveryStatus.Succeeded, delivery.Status);
        Assert.Equal(200, delivery.ResponseStatusCode);

        // Verify HTTP request sent
        var req = _handler.LastRequest;
        Assert.NotNull(req);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.Equal("https://receiver.example.com/events", req.RequestUri?.ToString());

        // Verify required headers
        Assert.True(req.Headers.Contains(WebhookHeaders.WebhookId));
        Assert.Equal("OrderCompletedEvent", req.Headers.GetValues(WebhookHeaders.WebhookEvent).First());
        Assert.True(req.Headers.Contains(WebhookHeaders.WebhookTimestamp));
        Assert.True(req.Headers.Contains(WebhookHeaders.WebhookSignature));

        // Verify custom headers
        Assert.Equal("Bearer test-token", req.Headers.GetValues("Authorization").First());
        Assert.Equal("Value", req.Headers.GetValues("X-Custom").First());

        // Verify envelope JSON
        var body = _handler.LastRequestBody;
        Assert.NotNull(body);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("OrderCompletedEvent", doc.RootElement.GetProperty("event").GetString());
        Assert.True(doc.RootElement.TryGetProperty("data", out var dataElem));
        Assert.Equal(99.50, dataElem.GetProperty("Amount").GetDouble());

        // Verify signature is valid
        var signature = req.Headers.GetValues(WebhookHeaders.WebhookSignature).First();
        var timestamp = req.Headers.GetValues(WebhookHeaders.WebhookTimestamp).First();
        Assert.True(_signer.Verify(subscription.Secret, timestamp, body, signature));
    }

    [Fact]
    public async Task DeliverAsync_HandlesFailedStatusCode()
    {
        _handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("Server Error")
        };

        var subscription = new WebhookSubscription
        {
            Id = DefaultId.NewUlid(),
            Url = "https://receiver.example.com/events",
            Secret = "secret-12345"
        };

        var webhookEvent = new WebhookEvent
        {
            Id = DefaultId.NewUlid(),
            EventType = "UserCreatedEvent",
            Payload = "{}"
        };

        var delivery = await _engine.DeliverAsync(subscription, webhookEvent, 1);

        Assert.Equal(DeliveryStatus.Failed, delivery.Status);
        Assert.Equal(500, delivery.ResponseStatusCode);
        Assert.Contains("500", delivery.Error);
    }

    [Fact]
    public async Task DeliverAsync_TruncatesLargeResponseBody()
    {
        var largeContent = new string('A', 10000);
        _handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(largeContent)
        };

        var subscription = new WebhookSubscription
        {
            Id = DefaultId.NewUlid(),
            Url = "https://receiver.example.com/events",
            Secret = "secret-12345"
        };

        var webhookEvent = new WebhookEvent
        {
            Id = DefaultId.NewUlid(),
            EventType = "UserCreatedEvent",
            Payload = "{}"
        };

        var delivery = await _engine.DeliverAsync(subscription, webhookEvent, 1);

        Assert.NotNull(delivery.ResponseBody);
        Assert.True(delivery.ResponseBody.Length <= _settings.MaxResponseBodyBytes);
    }

    [Fact]
    public async Task DeliverAsync_HandlesConnectionException()
    {
        _handler.ExceptionToThrow = new HttpRequestException("Connection refused");

        var subscription = new WebhookSubscription
        {
            Id = DefaultId.NewUlid(),
            Url = "https://receiver.example.com/events",
            Secret = "secret-12345"
        };

        var webhookEvent = new WebhookEvent
        {
            Id = DefaultId.NewUlid(),
            EventType = "UserCreatedEvent",
            Payload = "{}"
        };

        var delivery = await _engine.DeliverAsync(subscription, webhookEvent, 1);

        Assert.Equal(DeliveryStatus.Failed, delivery.Status);
        Assert.Null(delivery.ResponseStatusCode);
        Assert.Contains("Connection refused", delivery.Error);
    }
}
