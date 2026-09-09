# Webhooks — Module Agent Guide

This project is a self-contained **Modular Web API (MWA)** outbound Webhooks module. It provides outbound webhook delivery for MWA applications: subscription management, automatic domain event fan-out via the Kernel's `IEventBus`, HMAC-SHA256 signed payloads, configurable retry with exponential backoff, delivery logging, manual replay, SSRF protection, and dead-letter handling.

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

## Key Principles & Best Practices

1. **Fire-and-forget publishing:** Modules publish domain events normally via `IEventBus.PublishAsync()`. The `WebhookEventBridge` automatically captures them into `WebhookEvent` rows for background fan-out.
2. **Never reference Webhooks directly from other modules:** Reference `Webhooks.Contracts` only.
3. **Payload signing:** All payloads are signed with HMAC-SHA256 in the `X-Webhook-Signature` header computed over `{timestamp}.{payload}`. Receivers verify the signature and ensure `X-Webhook-Timestamp` is within 5 minutes.
4. **Loop prevention:** Webhook internal events (`WebhookDeliverySucceededEvent`, `WebhookDeliveryFailedEvent`, `WebhookDeliveryDeadLetteredEvent`, etc.) are ignored by `WebhookEventBridge` to prevent recursive delivery loops.
5. **SSRF protection:** Configured URL patterns (`BlockedUrlPatterns`, `AllowedUrlPatterns`) prevent delivery to private or loopback IP ranges in production.
