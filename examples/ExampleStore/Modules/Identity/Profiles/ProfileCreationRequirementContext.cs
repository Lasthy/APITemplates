using System.Diagnostics.CodeAnalysis;
using Kernel.Context;

namespace Identity.Profiles;

/// <summary>
/// Context passed to <see cref="IProfileCreationRequirement.EvaluateAsync"/> when evaluating
/// whether a user can be created with a specific profile.
/// </summary>
public class ProfileCreationRequirementContext
{
    /// <summary>
    /// The current caller's ambient scope context (authentication, roles, claims, correlation).
    /// </summary>
    public required IScopeContext ScopeContext { get; init; }

    /// <summary>
    /// The target profile descriptor being created.
    /// </summary>
    public required ProfileDescriptor Profile { get; init; }

    /// <summary>
    /// The email address provided in the user registration request, if available.
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    /// The username provided in the user registration request, if available.
    /// </summary>
    public string? UserName { get; init; }

    /// <summary>
    /// The scoped service provider for resolving dependencies during evaluation.
    /// </summary>
    public IServiceProvider? Services { get; init; }

    /// <summary>
    /// Optional additional contextual data or metadata associated with the creation attempt.
    /// </summary>
    public IReadOnlyDictionary<string, object>? AdditionalData { get; init; }

    /// <summary>
    /// Alias for <see cref="AdditionalData"/>.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Data => AdditionalData;

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
