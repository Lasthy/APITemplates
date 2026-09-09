namespace Webhooks.Contracts.Constants;

/// <summary>
/// Status states for webhook delivery attempts.
/// </summary>
public static class DeliveryStatus
{
    public const string Pending = "Pending";
    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";
    public const string DeadLettered = "DeadLettered";
}
