using System.Diagnostics.CodeAnalysis;
using Identity.Entities;
using Microsoft.AspNetCore.Identity;

namespace Identity.Profiles;

/// <summary>
/// Context passed to <see cref="IProfile.OnUserCreatedAsync"/> during user creation.
/// Provides access to the created user, the UserManager, the DI service provider
/// for resolving scoped services needed by the profile's creation hook, and any
/// additional data passed by the caller (useful for companion entities and dynamic claims).
/// </summary>
public class ProfileCreationContext
{
    /// <summary>
    /// The newly created user entity (already persisted with roles).
    /// </summary>
    public required ApplicationUser User { get; init; }

    /// <summary>
    /// The ASP.NET Identity UserManager for adding claims or modifying the user.
    /// </summary>
    public required UserManager<ApplicationUser> UserManager { get; init; }

    /// <summary>
    /// The scoped DI service provider for resolving module services.
    /// </summary>
    public required IServiceProvider Services { get; init; }

    /// <summary>
    /// Optional additional contextual data passed during user creation
    /// (e.g. payloads for companion entities, profile-specific fields, or metadata).
    /// </summary>
    public IReadOnlyDictionary<string, object>? AdditionalData { get; init; }

    /// <summary>
    /// Alias for <see cref="AdditionalData"/>.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Data => AdditionalData;

    /// <summary>
    /// Mutable dictionary for transient items shared during the creation hook lifecycle.
    /// </summary>
    public IDictionary<string, object?> Items { get; } = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Convenience method to add a claim to the user during creation.
    /// </summary>
    /// <param name="type">The claim type (e.g., "TenantId").</param>
    /// <param name="value">The claim value.</param>
    public async Task AddClaimAsync(string type, string value)
    {
        await UserManager.AddClaimAsync(User, new System.Security.Claims.Claim(type, value));
    }

    /// <summary>
    /// Checks whether <see cref="AdditionalData"/> contains the specified key (case-insensitive).
    /// </summary>
    public bool HasAdditionalData(string key) =>
        AdditionalData != null && AdditionalData.Keys.Any(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Retrieves a value from <see cref="AdditionalData"/> converted to <typeparamref name="T"/>,
    /// or default if not found or conversion fails. Supports case-insensitive key lookup.
    /// </summary>
    public T? GetAdditionalData<T>(string key)
    {
        TryGetAdditionalData<T>(key, out var value);
        return value;
    }

    /// <summary>
    /// Alias for <see cref="GetAdditionalData{T}"/>.
    /// </summary>
    public T? GetData<T>(string key) => GetAdditionalData<T>(key);

    /// <summary>
    /// Alias for <see cref="GetAdditionalData{T}"/>.
    /// </summary>
    public T? Get<T>(string key) => GetAdditionalData<T>(key);

    /// <summary>
    /// Attempts to retrieve and convert a value from <see cref="AdditionalData"/> with case-insensitive key lookup.
    /// Supports direct casting, JsonElement deserialization, Guid/Ulid parsing, and primitive type conversion.
    /// </summary>
    public bool TryGetAdditionalData<T>(string key, [MaybeNullWhen(false)] out T value) =>
        AdditionalDataHelper.TryGetValue(AdditionalData, key, Services, out value);

    /// <summary>
    /// Alias for <see cref="TryGetAdditionalData{T}"/>.
    /// </summary>
    public bool TryGetData<T>(string key, [MaybeNullWhen(false)] out T value) =>
        TryGetAdditionalData(key, out value);

    /// <summary>
    /// Alias for <see cref="TryGetAdditionalData{T}"/>.
    /// </summary>
    public bool TryGet<T>(string key, [MaybeNullWhen(false)] out T value) =>
        TryGetAdditionalData(key, out value);

    /// <summary>
    /// Gets raw additional data value by key using case-insensitive lookup, or null if not found.
    /// </summary>
    public object? this[string key]
    {
        get
        {
            if (AdditionalData == null || AdditionalData.Count == 0 || string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            if (AdditionalData.TryGetValue(key, out var exact))
            {
                return exact;
            }

            foreach (var kvp in AdditionalData)
            {
                if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value;
                }
            }

            return null;
        }
    }
}
