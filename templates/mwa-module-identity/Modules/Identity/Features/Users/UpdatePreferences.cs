using Kernel.Requests;

namespace Identity.Features.Users;

/// <summary>
/// Command to update a user's preferences JSON blob.
/// </summary>
public record UpdatePreferences(DefaultId UserId, string PreferencesJson) : IRequest<bool>;
