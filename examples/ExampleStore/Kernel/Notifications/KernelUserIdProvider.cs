using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace Kernel.Notifications;

/// <summary>
/// Custom <see cref="IUserIdProvider"/> that resolves user ID for SignalR from standard claims
/// (e.g. <see cref="ClaimTypes.NameIdentifier"/>, <c>sub</c>, or <see cref="ClaimTypes.Name"/>).
/// Matches <see cref="Kernel.Context.IScopeContext"/> conventions.
/// </summary>
public class KernelUserIdProvider : IUserIdProvider
{
    /// <inheritdoc />
    public string? GetUserId(HubConnectionContext connection)
    {
        return connection.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? connection.User?.FindFirst("sub")?.Value
            ?? connection.User?.FindFirst(ClaimTypes.Name)?.Value;
    }
}
