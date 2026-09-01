namespace Kernel.Email;

/// <summary>
/// Represents an email message to be sent via <see cref="IEmailSender"/> or queued into the outbox messaging system.
/// </summary>
public class EmailMessage
{
    /// <summary>
    /// Recipient email address(es). Multiple addresses can be comma- or semicolon-separated.
    /// </summary>
    public required string To { get; init; }

    /// <summary>
    /// Optional CC recipient email address(es).
    /// </summary>
    public string? Cc { get; init; }

    /// <summary>
    /// Optional BCC recipient email address(es).
    /// </summary>
    public string? Bcc { get; init; }

    /// <summary>
    /// Subject line of the email.
    /// </summary>
    public required string Subject { get; init; }

    /// <summary>
    /// Body content of the email (plain text or HTML).
    /// </summary>
    public required string Body { get; init; }

    /// <summary>
    /// Whether the body content is HTML (default: true).
    /// </summary>
    public bool IsHtml { get; init; } = true;

    /// <summary>
    /// Optional list of file attachments.
    /// </summary>
    public List<EmailAttachment>? Attachments { get; init; }
}
