using Kernel.Configuration;
using Kernel.Json;

namespace Kernel.Caching;

/// <summary>
/// Dynamic settings for caching, loaded from <c>appsettings.Cache.json</c>
/// with an optional environment overlay <c>appsettings.Cache.{Environment}.json</c>.
/// </summary>
public class CacheSettings : DynamicSettingsSection
{
    /// <inheritdoc />
    public override string SectionName => "Cache";

    /// <summary>
    /// Whether caching is enabled (default: true). If false, cache lookups and writes are bypassed.
    /// </summary>
    public bool Enabled =>
        !TryGetValue<bool>(nameof(Enabled), out var val) || val;

    /// <summary>
    /// Default expiration time in seconds for cached items when no explicit expiration is provided (default: 300).
    /// </summary>
    public int DefaultExpirationSeconds =>
        TryGetValue<int>(nameof(DefaultExpirationSeconds), out var val) && val > 0 ? val : 300;

    /// <summary>
    /// Optional global prefix prepended to all cache keys (default: empty string).
    /// </summary>
    public string KeyPrefix =>
        TryGetValue<string>(nameof(KeyPrefix), out var val) && !string.IsNullOrWhiteSpace(val) ? val : string.Empty;

    public CacheSettings(IJsonSerializer jsonSerializer, string? environment = null)
        : base("appsettings.Cache.json", jsonSerializer, environment)
    {
    }

    public CacheSettings(string source, IJsonSerializer jsonSerializer, string? environment = null)
        : base(source, jsonSerializer, environment)
    {
    }
}
