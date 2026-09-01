using Kernel.Testing.Fakes;
using Xunit;

namespace Kernel.Testing.Assertions;

/// <summary>
/// Fluent assertion extensions for <see cref="FakeNotificationService"/>.
/// </summary>
public static class NotificationAssertions
{
    /// <summary>
    /// Asserts that a notification was sent to the specified user.
    /// </summary>
    public static void ShouldHaveSentToUser(
        this FakeNotificationService service,
        DefaultId userId,
        string? channel = null,
        Func<object, bool>? payloadPredicate = null,
        string? userMessage = null)
    {
        ArgumentNullException.ThrowIfNull(service);

        Assert.True(
            service.HasSentToUser(userId, channel, payloadPredicate),
            userMessage ?? $"Expected notification to be sent to user '{userId}'{(channel != null ? $" on channel '{channel}'" : "")}, but none was found.");
    }

    /// <summary>
    /// Asserts that a notification was sent to the specified group.
    /// </summary>
    public static void ShouldHaveSentToGroup(
        this FakeNotificationService service,
        string group,
        string? channel = null,
        Func<object, bool>? payloadPredicate = null,
        string? userMessage = null)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(group);

        Assert.True(
            service.HasSentToGroup(group, channel, payloadPredicate),
            userMessage ?? $"Expected notification to be sent to group '{group}'{(channel != null ? $" on channel '{channel}'" : "")}, but none was found.");
    }

    /// <summary>
    /// Asserts that a broadcast notification was sent to all clients.
    /// </summary>
    public static void ShouldHaveSentBroadcast(
        this FakeNotificationService service,
        string? channel = null,
        Func<object, bool>? payloadPredicate = null,
        string? userMessage = null)
    {
        ArgumentNullException.ThrowIfNull(service);

        Assert.True(
            service.HasSentBroadcast(channel, payloadPredicate),
            userMessage ?? $"Expected broadcast notification{(channel != null ? $" on channel '{channel}'" : "")}, but none was found.");
    }

    /// <summary>
    /// Asserts that no notifications were sent through the service.
    /// </summary>
    public static void ShouldNotHaveSentNotifications(
        this FakeNotificationService service,
        string? userMessage = null)
    {
        ArgumentNullException.ThrowIfNull(service);

        Assert.True(
            service.SentNotifications.Count == 0,
            userMessage ?? $"Expected no notifications to have been sent, but found {service.SentNotifications.Count}.");
    }

    /// <summary>
    /// Asserts that exactly <paramref name="expectedCount"/> notifications were sent through the service.
    /// </summary>
    public static void ShouldHaveNotificationCount(
        this FakeNotificationService service,
        int expectedCount,
        string? userMessage = null)
    {
        ArgumentNullException.ThrowIfNull(service);

        Assert.Equal(
            expectedCount,
            service.SentNotifications.Count);
    }
}
