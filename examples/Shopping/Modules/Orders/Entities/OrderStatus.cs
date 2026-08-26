namespace Orders.Entities;

/// <summary>
/// Status constants for customer orders.
/// </summary>
public static class OrderStatus
{
    public const string Pending = "Pending";
    public const string Paid = "Paid";
    public const string Sent = "Sent";
    public const string Delivered = "Delivered";
    public const string Cancelled = "Cancelled";
}
