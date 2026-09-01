using Kernel.Common;
using Kernel.Email;

namespace Kernel.Testing.Fakes;

/// <summary>
/// A test fake implementing <see cref="IEmailSender"/> that records all sent email messages in-memory.
/// Supports simulating email send success/failure outcomes.
/// </summary>
public class FakeEmailSender : IEmailSender
{
    private readonly List<EmailMessage> _sentMessages = [];
    private readonly object _lock = new();

    /// <summary>
    /// Gets all email messages sent through this fake in chronological order.
    /// </summary>
    public IReadOnlyList<EmailMessage> SentMessages
    {
        get
        {
            lock (_lock)
            {
                return _sentMessages.ToList();
            }
        }
    }

    /// <summary>
    /// Gets the most recently sent email message, or null if no emails have been sent.
    /// </summary>
    public EmailMessage? LastSentMessage
    {
        get
        {
            lock (_lock)
            {
                return _sentMessages.LastOrDefault();
            }
        }
    }

    /// <summary>
    /// Gets the total number of sent email messages.
    /// </summary>
    public int SentCount
    {
        get
        {
            lock (_lock)
            {
                return _sentMessages.Count;
            }
        }
    }

    /// <summary>
    /// Controls whether <see cref="SendAsync"/> should return a success result. Defaults to <c>true</c>.
    /// </summary>
    public bool ShouldSucceed { get; set; } = true;

    /// <summary>
    /// Custom exception to return when <see cref="ShouldSucceed"/> is set to <c>false</c>.
    /// </summary>
    public Exception? FailureError { get; set; }

    /// <summary>
    /// Records a sent email message in-memory, or returns a configured failure.
    /// </summary>
    public Task<Result> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (!ShouldSucceed)
        {
            var error = FailureError ?? new InvalidOperationException("Simulated email sending failure.");
            return Task.FromResult(Result.Failure(error));
        }

        lock (_lock)
        {
            _sentMessages.Add(message);
        }

        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Checks whether an email matching the given predicate was sent.
    /// </summary>
    public bool HasSent(Func<EmailMessage, bool>? predicate = null)
    {
        lock (_lock)
        {
            return predicate == null
                ? _sentMessages.Count > 0
                : _sentMessages.Any(predicate);
        }
    }

    /// <summary>
    /// Gets all emails sent to a specific recipient address (checking To, Cc, and Bcc).
    /// </summary>
    public IReadOnlyList<EmailMessage> GetSentTo(string emailAddress)
    {
        ArgumentNullException.ThrowIfNull(emailAddress);
        lock (_lock)
        {
            return _sentMessages
                .Where(m =>
                    string.Equals(m.To, emailAddress, StringComparison.OrdinalIgnoreCase) ||
                    (m.Cc != null && m.Cc.Contains(emailAddress, StringComparison.OrdinalIgnoreCase)) ||
                    (m.Bcc != null && m.Bcc.Contains(emailAddress, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }
    }

    /// <summary>
    /// Clears all recorded sent emails and resets failure configurations.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _sentMessages.Clear();
            ShouldSucceed = true;
            FailureError = null;
        }
    }
}
