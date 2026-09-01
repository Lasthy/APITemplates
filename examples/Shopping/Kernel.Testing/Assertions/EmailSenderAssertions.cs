using Kernel.Email;
using Kernel.Testing.Fakes;
using Xunit;

namespace Kernel.Testing.Assertions;

/// <summary>
/// Fluent assertion extensions for <see cref="FakeEmailSender"/>.
/// </summary>
public static class EmailSenderAssertions
{
    /// <summary>
    /// Asserts that at least one email was sent.
    /// </summary>
    /// <returns>The last sent email message.</returns>
    public static EmailMessage ShouldHaveSent(this FakeEmailSender sender, string? userMessage = null)
    {
        ArgumentNullException.ThrowIfNull(sender);
        Assert.True(
            sender.SentCount > 0,
            userMessage ?? "Expected at least one email to have been sent, but none was recorded.");
        return sender.LastSentMessage!;
    }

    /// <summary>
    /// Asserts that an email matching the given predicate was sent.
    /// </summary>
    /// <returns>The first matching sent email message.</returns>
    public static EmailMessage ShouldHaveSent(
        this FakeEmailSender sender,
        Func<EmailMessage, bool> predicate,
        string? userMessage = null)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(predicate);

        var matching = sender.SentMessages.FirstOrDefault(predicate);
        Assert.NotNull(matching);
        return matching;
    }

    /// <summary>
    /// Asserts that an email was sent to the specified recipient address.
    /// </summary>
    /// <returns>The first email sent to that address.</returns>
    public static EmailMessage ShouldHaveSentTo(
        this FakeEmailSender sender,
        string emailAddress,
        string? userMessage = null)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(emailAddress);

        var emails = sender.GetSentTo(emailAddress);
        Assert.True(
            emails.Count > 0,
            userMessage ?? $"Expected an email sent to '{emailAddress}', but none was found.");
        return emails[0];
    }

    /// <summary>
    /// Asserts that exactly <paramref name="expectedCount"/> emails were sent.
    /// </summary>
    public static IReadOnlyList<EmailMessage> ShouldHaveSentCount(
        this FakeEmailSender sender,
        int expectedCount,
        string? userMessage = null)
    {
        ArgumentNullException.ThrowIfNull(sender);
        Assert.True(
            sender.SentCount == expectedCount,
            userMessage ?? $"Expected {expectedCount} email(s) to have been sent, but found {sender.SentCount}.");
        return sender.SentMessages;
    }

    /// <summary>
    /// Asserts that no emails were sent.
    /// </summary>
    public static void ShouldNotHaveSent(this FakeEmailSender sender, string? userMessage = null)
    {
        ArgumentNullException.ThrowIfNull(sender);
        Assert.True(
            sender.SentCount == 0,
            userMessage ?? $"Expected no emails to have been sent, but found {sender.SentCount}.");
    }

    /// <summary>
    /// Asserts that no email matching the given predicate was sent.
    /// </summary>
    public static void ShouldNotHaveSent(
        this FakeEmailSender sender,
        Func<EmailMessage, bool> predicate,
        string? userMessage = null)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(predicate);

        var matching = sender.SentMessages.Where(predicate).ToList();
        Assert.True(
            matching.Count == 0,
            userMessage ?? $"Expected no matching emails to have been sent, but found {matching.Count}.");
    }
}
