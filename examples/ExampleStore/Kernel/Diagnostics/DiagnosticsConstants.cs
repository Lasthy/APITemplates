using System.Diagnostics;

namespace Kernel.Diagnostics;

/// <summary>
/// Constants and static diagnostic sources used across Kernel diagnostics and observability.
/// </summary>
public static class DiagnosticsConstants
{
    /// <summary>
    /// Standard HTTP header name for request correlation tracking ("X-Correlation-Id").
    /// </summary>
    public const string CorrelationIdHeader = "X-Correlation-Id";

    /// <summary>
    /// Key used in <see cref="Microsoft.AspNetCore.Http.HttpContext.Items"/> to store the correlation ID.
    /// </summary>
    public const string CorrelationIdItemKey = "CorrelationId";

    /// <summary>
    /// OpenTelemetry ActivitySource name for the Kernel.
    /// </summary>
    public const string ActivitySourceName = "Kernel";

    /// <summary>
    /// OpenTelemetry tag key for correlation ID.
    /// </summary>
    public const string CorrelationIdTag = "correlation.id";

    /// <summary>
    /// ActivitySource instance used for creating tracing activities in Kernel operations.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, "1.0.0");
}
