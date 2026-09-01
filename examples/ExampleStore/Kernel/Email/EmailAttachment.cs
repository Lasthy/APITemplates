namespace Kernel.Email;

/// <summary>
/// Represents a file attachment for an <see cref="EmailMessage"/>.
/// </summary>
public class EmailAttachment
{
    /// <summary>
    /// File name of the attachment including extension (e.g. "invoice.pdf").
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// Binary content of the attachment represented as a byte array.
    /// </summary>
    public required byte[] Content { get; init; }

    /// <summary>
    /// Optional MIME content type (e.g. "application/pdf", "image/png").
    /// </summary>
    public string? ContentType { get; init; }
}
