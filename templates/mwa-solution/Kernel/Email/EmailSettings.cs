using Kernel.Configuration;
using Kernel.Json;

namespace Kernel.Email;

/// <summary>
/// Dynamic settings for email delivery, loaded from <c>appsettings.Email.json</c>
/// with an optional environment overlay <c>appsettings.Email.{Environment}.json</c>.
/// </summary>
public class EmailSettings : DynamicSettingsSection
{
    /// <inheritdoc />
    public override string SectionName => "Email";

    /// <summary>
    /// SMTP server hostname or IP address (default: "localhost").
    /// </summary>
    public string SmtpHost =>
        TryGetValue<string>(nameof(SmtpHost), out var val) && !string.IsNullOrWhiteSpace(val)
            ? val
            : "localhost";

    /// <summary>
    /// SMTP server port (default: 587).
    /// </summary>
    public int SmtpPort =>
        TryGetValue<int>(nameof(SmtpPort), out var val) && val > 0 ? val : 587;

    /// <summary>
    /// Optional username for SMTP authentication.
    /// </summary>
    public string? SmtpUsername =>
        TryGetValue<string>(nameof(SmtpUsername), out var val) ? val : null;

    /// <summary>
    /// Optional password for SMTP authentication.
    /// </summary>
    public string? SmtpPassword =>
        TryGetValue<string>(nameof(SmtpPassword), out var val) ? val : null;

    /// <summary>
    /// Whether SSL/TLS is enabled for SMTP communication (default: true).
    /// </summary>
    public bool EnableSsl =>
        !TryGetValue<bool>(nameof(EnableSsl), out var val) || val;

    /// <summary>
    /// Default sender email address (From header, default: "noreply@example.com").
    /// </summary>
    public string FromAddress =>
        TryGetValue<string>(nameof(FromAddress), out var val) && !string.IsNullOrWhiteSpace(val)
            ? val
            : "noreply@example.com";

    /// <summary>
    /// Default sender display name (From header, default: "MyAPI").
    /// </summary>
    public string FromName =>
        TryGetValue<string>(nameof(FromName), out var val) && !string.IsNullOrWhiteSpace(val)
            ? val
            : "MyAPI";

    public EmailSettings(IJsonSerializer jsonSerializer, string? environment = null)
        : base("appsettings.Email.json", jsonSerializer, environment)
    {
    }
}
