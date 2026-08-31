using Kernel.Common;

namespace Kernel.Email;

/// <summary>
/// Abstraction for sending email messages.
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// Sends an email message asynchronously.
    /// </summary>
    /// <param name="message">The email message to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="Result"/> indicating success or failure.</returns>
    Task<Result> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
