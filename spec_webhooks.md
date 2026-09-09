# Webhooks Module — Specification

**Template:** `mwa-module-webhooks` · **Short Name:** `modular-webapi-module-webhooks` · **Type:** Project

This module provides outbound webhook delivery for MWA applications: subscription management, automatic event fan-out via the Kernel's `IEventBus`, HMAC-SHA256 signed payloads, configurable retry with exponential backoff, delivery logging, manual replay, and dead-letter handling.

---

## Module Anatomy

```
Modules/
├── Webhooks/                                         # Module implementation
│   ├── Configuration/
│   │   └── WebhookSettings.cs                       # DynamicSettingsSection: retry, timeout, security
│   ├── Controllers/
│   │   ├── SubscriptionsController.cs               # Subscription CRUD, pause/resume, rotate secret
│   │   └── DeliveriesController.cs                  # Delivery logs, replay, stats
│   ├── Entities/
│   │   ├── WebhookSubscription.cs                   # Subscription (URL, secret, event filters, status)
│   │   ├── WebhookDelivery.cs                       # Individual delivery attempt record
│   │   └── WebhookEvent.cs                          # Queued event pending fan-out
│   ├── Features/
│   │   ├── Subscriptions/                           # CreateSubscription, GetSubscriptions, GetSubscription, UpdateSubscription, DeleteSubscription, PauseSubscription, ResumeSubscription, RotateSecret, TestSubscription
│   │   ├── Deliveries/                              # GetDeliveries, GetDelivery, ReplayDelivery, GetDeliveryStats
│   │   └── GetSubscriptionSummaryHandler.cs         # Cross-module handler for GetSubscriptionSummary
│   ├── Delivery/
│   │   ├── IWebhookDeliveryEngine.cs                # Core delivery abstraction
│   │   ├── WebhookDeliveryEngine.cs                 # HttpClient-based delivery with signing
│   │   ├── ISignatureService.cs                     # Payload signing abstraction
│   │   └── HmacSignatureService.cs                  # HMAC-SHA256 implementation
│   ├── EventBridge/
│   │   └── WebhookEventBridge.cs                    # IEventHandler<> that captures domain events → WebhookEvent queue
│   ├── Persistence/
│   │   └── WebhookModuleDbContext.cs                # DbContext with "webhooks" schema
│   ├── Services/
│   │   ├── IWebhookService.cs                       # Subscription management operations
│   │   └── WebhookService.cs                        # Implementation
│   ├── Workers/
│   │   ├── WebhookDeliveryWorker.cs                 # Processes queued WebhookEvents, fans out to subscriptions
│   │   └── DeliveryCleanupWorker.cs                 # Purges old delivery logs past retention
│   ├── WebhooksModule.cs                            # IModule entry point
│   ├── appsettings.Webhooks.json                    # Module settings
│   └── appsettings.Webhooks.Development.json        # Dev overlay
│
└── Webhooks.Contracts/                               # Companion contracts
    ├── Constants/
    │   ├── WebhookPolicies.cs                       # ManageWebhooks
    │   ├── WebhookHeaders.cs                        # X-Webhook-Id, X-Webhook-Signature, X-Webhook-Timestamp
    │   └── DeliveryStatus.cs                        # Pending, Succeeded, Failed, DeadLettered
    ├── Dtos/
    │   ├── WebhookSubscriptionDto.cs                # Summary DTO
    │   ├── WebhookSubscriptionDetailDto.cs          # Full DTO with event filters, delivery stats
    │   ├── WebhookDeliveryDto.cs                    # Delivery attempt summary
    │   ├── WebhookDeliveryDetailDto.cs              # Full delivery with request/response bodies
    │   └── DeliveryStatsDto.cs                      # Aggregate stats (total, succeeded, failed, avg latency)
    ├── Events/
    │   ├── WebhookDeliverySucceededEvent.cs          # (SubscriptionId, DeliveryId, EventType)
    │   ├── WebhookDeliveryFailedEvent.cs             # (SubscriptionId, DeliveryId, EventType, StatusCode, Error)
    │   ├── WebhookDeliveryDeadLetteredEvent.cs       # (SubscriptionId, DeliveryId, EventType, AttemptCount)
    │   ├── WebhookSubscriptionCreatedEvent.cs        # (SubscriptionId, Url, EventTypes)
    │   └── WebhookSubscriptionPausedEvent.cs         # (SubscriptionId, Reason)
    ├── Requests/
    │   └── GetSubscriptionSummary.cs                 # IRequest<WebhookSubscriptionDto>
    └── Webhooks.Contracts.csproj
```

---

## 1. Entities

### `WebhookSubscription`

```csharp
public class WebhookSubscription : IAuditable, ISoftDeletable
{
    public DefaultId Id { get; set; }

    /// <summary>
    /// Human-readable name for this subscription (e.g., "Stripe payment sync").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Target URL to receive webhook POST requests.
    /// Must be HTTPS in production.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// HMAC-SHA256 secret used to sign payloads.
    /// Generated on creation, rotatable via endpoint.
    /// </summary>
    [Encrypted]
    public string Secret { get; set; } = string.Empty;

    /// <summary>
    /// Comma-separated list of event type names this subscription listens to.
    /// Empty/null means "all events" (wildcard).
    /// Example: "UserCreatedEvent,OrderCompletedEvent"
    /// </summary>
    public string? EventTypes { get; set; }

    /// <summary>
    /// Current status: Active, Paused, Disabled (auto-disabled after too many failures).
    /// </summary>
    public string Status { get; set; } = "Active";

    /// <summary>
    /// Optional description for administrative purposes.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Optional custom HTTP headers to include in deliveries (JSON object).
    /// Example: {"Authorization": "Bearer xyz"}
    /// </summary>
    public string? CustomHeadersJson { get; set; }

    /// <summary>
    /// The user who owns this subscription.
    /// </summary>
    public DefaultId OwnerUserId { get; set; }

    /// <summary>
    /// Consecutive failure count. Used for auto-disable logic.
    /// Resets to 0 on successful delivery.
    /// </summary>
    public int ConsecutiveFailures { get; set; }

    /// <summary>
    /// Total successful deliveries (lifetime counter).
    /// </summary>
    public long TotalDeliveries { get; set; }

    /// <summary>
    /// Total failed deliveries (lifetime counter).
    /// </summary>
    public long TotalFailures { get; set; }

    /// <summary>
    /// Timestamp of the last successful delivery.
    /// </summary>
    public DateTime? LastDeliveryAt { get; set; }

    // ── IAuditable ──
    public DateTime CreatedAt { get; set; }
    public DefaultId? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DefaultId? UpdatedBy { get; set; }

    // ── ISoftDeletable ──
    public DateTime? DeletedAt { get; set; }
    public DefaultId? DeletedBy { get; set; }
}
```

### `WebhookDelivery`

```csharp
public class WebhookDelivery
{
    public DefaultId Id { get; set; }
    public DefaultId SubscriptionId { get; set; }
    public DefaultId WebhookEventId { get; set; }

    /// <summary>
    /// The CLR type name of the event that was delivered.
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Delivery status: Pending, Succeeded, Failed, DeadLettered.
    /// </summary>
    public string Status { get; set; } = "Pending";

    /// <summary>
    /// HTTP status code returned by the receiver (null if connection failed).
    /// </summary>
    public int? ResponseStatusCode { get; set; }

    /// <summary>
    /// Response body (truncated to max 4KB for logging).
    /// </summary>
    public string? ResponseBody { get; set; }

    /// <summary>
    /// The serialized JSON payload that was sent.
    /// </summary>
    public string RequestBody { get; set; } = string.Empty;

    /// <summary>
    /// Error message if delivery failed (connection error, timeout, etc.).
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Current attempt number (1-based).
    /// </summary>
    public int AttemptNumber { get; set; } = 1;

    /// <summary>
    /// Round-trip duration in milliseconds.
    /// </summary>
    public long? DurationMs { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? NextRetryAt { get; set; }

    // Navigation
    public WebhookSubscription Subscription { get; set; } = null!;
}
```

### `WebhookEvent`

```csharp
public class WebhookEvent
{
    public DefaultId Id { get; set; }

    /// <summary>
    /// CLR type name of the original domain event.
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Serialized JSON of the original domain event.
    /// </summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>
    /// Whether this event has been fully processed (delivered to all matching subscriptions).
    /// </summary>
    public bool IsProcessed { get; set; }

    public DateTime OccurredAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}
```

---

## 2. Event Bridge — Automatic Fan-Out

The core design: domain events published anywhere in the application via `IEventBus.PublishAsync<T>()` are automatically captured and queued for webhook delivery. No module needs to know about webhooks.

### `WebhookEventBridge`

An `IEventHandler<Event>` registered for the **base `Event` type**. The Kernel's `InProcessEventBus` invokes it for every published event.

**Behavior:**
1. Receives every domain event.
2. Serializes it to JSON via `IJsonSerializer`.
3. Persists a `WebhookEvent` row.
4. The `WebhookDeliveryWorker` picks it up asynchronously.

> **Why queue instead of delivering inline?** Webhook delivery involves external HTTP calls that can be slow, fail, or timeout. Queuing decouples event publishing (fast, in-process) from delivery (async, retryable). This is conceptually aligned with the Kernel's outbox pattern.

### Event Type Filtering

When the worker processes a `WebhookEvent`, it queries `WebhookSubscription` where:
- `Status = "Active"`
- `EventTypes IS NULL` (wildcard) **OR** `EventTypes` contains the event's type name

---

## 3. Delivery Engine

### `IWebhookDeliveryEngine`

```csharp
public interface IWebhookDeliveryEngine
{
    Task<WebhookDelivery> DeliverAsync(WebhookSubscription subscription, WebhookEvent webhookEvent, int attemptNumber, CancellationToken ct = default);
}
```

### Payload Format

Every webhook delivery is a `POST` request with:

```http
POST {subscription.Url} HTTP/1.1
Content-Type: application/json
X-Webhook-Id: {delivery.Id}
X-Webhook-Event: {event.EventType}
X-Webhook-Timestamp: {unix timestamp}
X-Webhook-Signature: sha256={hex(HMAC-SHA256(secret, timestamp.payload))}
{custom headers from subscription, if any}

{
  "id": "01JRXYZ...",
  "event": "UserCreatedEvent",
  "timestamp": "2026-09-08T13:00:00Z",
  "data": { ... original event properties ... }
}
```

### Signature Verification (Receiver Side)

The `X-Webhook-Signature` header contains `sha256={hex}` where the HMAC is computed over `{timestamp}.{payload_body}`. Receivers should:
1. Extract the timestamp from `X-Webhook-Timestamp`.
2. Reject if the timestamp is older than 5 minutes (replay protection).
3. Compute `HMAC-SHA256(secret, "{timestamp}.{body}")` and compare to the signature.

### `ISignatureService`

```csharp
public interface ISignatureService
{
    string Sign(string secret, string timestamp, string payload);
    bool Verify(string secret, string timestamp, string payload, string signature);
}
```

---

## 4. Retry & Dead-Letter Strategy

### Retry Policy

Failed deliveries are retried with **exponential backoff**:

| Attempt | Delay |
|---|---|
| 1 | Immediate |
| 2 | 30 seconds |
| 3 | 2 minutes |
| 4 | 15 minutes |
| 5 | 1 hour |
| 6+ | Dead-lettered |

Configurable via `WebhookSettings`:
- `MaxRetries` (default: `5`)
- `InitialRetryDelaySeconds` (default: `30`)
- `BackoffMultiplier` (default: `4.0`)
- `MaxRetryDelayMinutes` (default: `60`)

### Success Criteria

A delivery is considered successful if:
- HTTP status code is `2xx`
- Response is received within `DeliveryTimeoutSeconds` (default: `30`)

### Auto-Disable

If a subscription accumulates `MaxConsecutiveFailures` (default: `10`) consecutive failures, it is automatically set to `Status = "Disabled"` and a `WebhookSubscriptionPausedEvent` is published. The subscription must be manually resumed via API.

### Dead-Letter

After exhausting all retries, the delivery is marked as `DeadLettered`. It remains in the database for auditing and can be manually replayed via the API.

---

## 5. Persistence

### `WebhookModuleDbContext`

```csharp
public class WebhookModuleDbContext : DbContext, IDbContextSchema
{
    public string Schema => "webhooks";

    public DbSet<WebhookSubscription> Subscriptions { get; set; } = null!;
    public DbSet<WebhookDelivery> Deliveries { get; set; } = null!;
    public DbSet<WebhookEvent> Events { get; set; } = null!;
}
```

**Key indexes:**
- `WebhookSubscription.OwnerUserId`
- `WebhookSubscription.Status`
- `WebhookDelivery.SubscriptionId`
- `WebhookDelivery.Status`
- `WebhookDelivery.NextRetryAt` — for worker to efficiently query retryable deliveries
- `WebhookEvent.IsProcessed` — for worker to query unprocessed events
- `WebhookEvent.OccurredAt`

---

## 6. Configuration

### `appsettings.Webhooks.json`

```json
{
  "MaxRetries": 5,
  "InitialRetryDelaySeconds": 30,
  "BackoffMultiplier": 4.0,
  "MaxRetryDelayMinutes": 60,
  "DeliveryTimeoutSeconds": 30,
  "MaxConsecutiveFailures": 10,
  "ProcessingIntervalSeconds": 5,
  "CleanupRetentionDays": 30,
  "CleanupIntervalHours": 24,
  "MaxSubscriptionsPerUser": 20,
  "MaxResponseBodyBytes": 4096,
  "RequireHttps": true,
  "BlockedUrlPatterns": ["*.internal", "localhost", "127.0.0.1", "10.*", "172.16.*", "192.168.*"],
  "AllowedUrlPatterns": []
}
```

| Key | Type | Default | Purpose |
|---|---|---|---|
| `MaxRetries` | `int` | `5` | Max delivery attempts before dead-lettering |
| `InitialRetryDelaySeconds` | `int` | `30` | First retry delay |
| `BackoffMultiplier` | `double` | `4.0` | Exponential backoff multiplier |
| `MaxRetryDelayMinutes` | `int` | `60` | Cap on retry delay |
| `DeliveryTimeoutSeconds` | `int` | `30` | HTTP request timeout per delivery |
| `MaxConsecutiveFailures` | `int` | `10` | Consecutive failures before auto-disable |
| `ProcessingIntervalSeconds` | `int` | `5` | Worker polling interval |
| `CleanupRetentionDays` | `int` | `30` | Days to keep delivery logs before purging |
| `CleanupIntervalHours` | `int` | `24` | Cleanup worker interval |
| `MaxSubscriptionsPerUser` | `int` | `20` | Max active subscriptions per user |
| `MaxResponseBodyBytes` | `int` | `4096` | Max response body stored per delivery |
| `RequireHttps` | `bool` | `true` | Reject non-HTTPS subscription URLs in production |
| `BlockedUrlPatterns` | `string[]` | `[internal IPs]` | URL patterns that cannot be used (SSRF protection) |
| `AllowedUrlPatterns` | `string[]` | `[]` | If non-empty, only these patterns are allowed |

---

## 7. API Endpoints

### Subscriptions (`/api/v1/webhooks/subscriptions`)

| Verb | Path | Auth | Description |
|---|---|---|---|
| `POST` | `/` | Authenticated | Create a webhook subscription |
| `GET` | `/` | Authenticated / System | List subscriptions (own for users, all for System) |
| `GET` | `/{id}` | Owner / System | Get subscription details with delivery stats |
| `PUT` | `/{id}` | Owner / System | Update URL, event types, custom headers, name |
| `DELETE` | `/{id}` | Owner / System | Soft-delete a subscription |
| `POST` | `/{id}/pause` | Owner / System | Pause delivery to this subscription |
| `POST` | `/{id}/resume` | Owner / System | Resume a paused/disabled subscription |
| `POST` | `/{id}/rotate-secret` | Owner / System | Generate a new HMAC secret (returns new secret once) |
| `POST` | `/{id}/test` | Owner / System | Send a test event (`WebhookTestEvent`) to verify connectivity |

### Deliveries (`/api/v1/webhooks/deliveries`)

| Verb | Path | Auth | Description |
|---|---|---|---|
| `GET` | `/` | Authenticated / System | List deliveries (filterable by subscription, status, date range) |
| `GET` | `/{id}` | Owner / System | Full delivery details (request body, response, timing) |
| `POST` | `/{id}/replay` | Owner / System | Replay a failed or dead-lettered delivery |
| `GET` | `/stats` | Authenticated / System | Aggregate delivery statistics |

---

## 8. Cross-Module Contracts

### DTOs

```csharp
public record WebhookSubscriptionDto(DefaultId Id, string Name, string Url, string Status, string? EventTypes, DateTime CreatedAt);
public record WebhookSubscriptionDetailDto(DefaultId Id, string Name, string Url, string Status, string? EventTypes,
    string? Description, long TotalDeliveries, long TotalFailures, int ConsecutiveFailures,
    DateTime? LastDeliveryAt, DateTime CreatedAt);
public record WebhookDeliveryDto(DefaultId Id, DefaultId SubscriptionId, string EventType, string Status,
    int? ResponseStatusCode, int AttemptNumber, long? DurationMs, DateTime CreatedAt);
public record WebhookDeliveryDetailDto(DefaultId Id, DefaultId SubscriptionId, string EventType, string Status,
    int? ResponseStatusCode, string? ResponseBody, string RequestBody, string? Error,
    int AttemptNumber, long? DurationMs, DateTime CreatedAt, DateTime? CompletedAt, DateTime? NextRetryAt);
public record DeliveryStatsDto(long TotalDeliveries, long Succeeded, long Failed, long DeadLettered, long Pending, double AvgDurationMs);
```

### Events

```csharp
public record WebhookDeliverySucceededEvent(DefaultId SubscriptionId, DefaultId DeliveryId, string EventType) : Event;
public record WebhookDeliveryFailedEvent(DefaultId SubscriptionId, DefaultId DeliveryId, string EventType, int? StatusCode, string Error) : Event;
public record WebhookDeliveryDeadLetteredEvent(DefaultId SubscriptionId, DefaultId DeliveryId, string EventType, int AttemptCount) : Event;
public record WebhookSubscriptionCreatedEvent(DefaultId SubscriptionId, string Url, string? EventTypes) : Event;
public record WebhookSubscriptionPausedEvent(DefaultId SubscriptionId, string Reason) : Event;
```

### Cross-Module Requests

```csharp
public record GetSubscriptionSummary(DefaultId SubscriptionId) : IRequest<WebhookSubscriptionDto>;
```

---

## 9. Workers

### `WebhookDeliveryWorker`

Periodic `BaseWorker` running at `ProcessingIntervalSeconds` interval:

1. **Process new events:** Query `WebhookEvent` where `IsProcessed = false`. For each, find matching active subscriptions and create `WebhookDelivery` records, then mark the event as processed.
2. **Process pending deliveries:** Query `WebhookDelivery` where `Status = "Pending"` or (`Status = "Failed"` and `NextRetryAt <= now`). For each, invoke `IWebhookDeliveryEngine.DeliverAsync()`.
3. **Handle results:** On success, update status and reset `ConsecutiveFailures` on the subscription. On failure, increment attempt, calculate next retry, or dead-letter.

### `DeliveryCleanupWorker`

Periodic `BaseWorker` running at `CleanupIntervalHours` interval. Deletes `WebhookDelivery` rows older than `CleanupRetentionDays` and processed `WebhookEvent` rows.

---

## 10. Security Considerations

1. **SSRF Protection:** `BlockedUrlPatterns` prevents subscriptions targeting internal network addresses. Always enabled in production.
2. **HTTPS Enforcement:** `RequireHttps` rejects `http://` URLs in non-Development environments.
3. **Secret Storage:** Subscription secrets are encrypted at rest using the Kernel's `[Encrypted]` attribute.
4. **Signature Replay Protection:** Receivers should validate `X-Webhook-Timestamp` is within 5 minutes to prevent replay attacks.
5. **Response Body Truncation:** Response bodies are truncated to `MaxResponseBodyBytes` to prevent storage abuse from verbose receivers.

---

## 11. Rules & Best Practices

1. **Webhooks are fire-and-forget from the publisher's perspective.** Modules publish events normally via `IEventBus` — they don't know about webhooks.
2. **Never reference Webhooks implementation from other modules.** Reference `Webhooks.Contracts` only.
3. **The `WebhookEventBridge` is opt-out, not opt-in.** All events are captured by default. Use `EventTypes` on subscriptions to filter what gets delivered.
4. **Test endpoint before going live.** Use `POST /subscriptions/{id}/test` to verify connectivity and signature validation.
5. **Monitor `ConsecutiveFailures`.** Subscriptions with high failure counts indicate endpoint issues. The auto-disable mechanism prevents flooding broken endpoints.
6. **Replay sparingly.** Replay re-delivers the exact same payload. Ensure your receivers are idempotent.
7. **Webhook events should not trigger more webhooks.** The `WebhookDeliverySucceededEvent` and other webhook-module events are excluded from the event bridge to prevent infinite loops.
