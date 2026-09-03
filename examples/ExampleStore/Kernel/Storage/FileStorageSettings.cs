using Kernel.Configuration;
using Kernel.Json;

namespace Kernel.Storage;

/// <summary>
/// Dynamic settings for file and blob storage, loaded from <c>appsettings.Storage.json</c>
/// with optional environment overlay <c>appsettings.Storage.{Environment}.json</c>.
/// </summary>
public class FileStorageSettings : DynamicSettingsSection
{
    private const string DefaultProvider = "Local";
    private const string DefaultBasePath = "./storage";
    private const bool DefaultEnablePublicAccess = true;
    private const long DefaultMaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    /// <inheritdoc />
    public override string SectionName => "Storage";

    /// <summary>
    /// The storage provider implementation to use (e.g. "Local", "S3", "AzureBlob").
    /// </summary>
    public string Provider =>
        TryGetValue<string>(nameof(Provider), out var val) && !string.IsNullOrWhiteSpace(val) ? val : DefaultProvider;

    /// <summary>
    /// The base directory path for local file storage.
    /// Can be relative to application root or an absolute filesystem path.
    /// </summary>
    public string BasePath =>
        TryGetValue<string>(nameof(BasePath), out var val) && !string.IsNullOrWhiteSpace(val) ? val : DefaultBasePath;

    /// <summary>
    /// Whether public file retrieval via generated file URLs is enabled.
    /// </summary>
    public bool EnablePublicAccess =>
        !TryGetValue<bool>(nameof(EnablePublicAccess), out var val) || val;

    /// <summary>
    /// Maximum allowed file size in bytes for uploads. Set to 0 to disable size checks.
    /// </summary>
    public long MaxFileSizeBytes =>
        TryGetValue<long>(nameof(MaxFileSizeBytes), out var val) ? val : DefaultMaxFileSizeBytes;

    /// <summary>
    /// Allowed file extensions (e.g. [".jpg", ".png", ".pdf"]). If empty, all extensions are allowed.
    /// </summary>
    public IReadOnlyList<string> AllowedExtensions =>
        TryGetValue<List<string>>(nameof(AllowedExtensions), out var val) && val != null ? val : [];

    /// <summary>
    /// Whether to automatically create destination directories if they don't exist.
    /// </summary>
    public bool AutoCreateDirectory =>
        !TryGetValue<bool>(nameof(AutoCreateDirectory), out var val) || val;

    public FileStorageSettings(IJsonSerializer jsonSerializer, string? environment = null)
        : base("appsettings.Storage.json", jsonSerializer, environment)
    {
    }

    public FileStorageSettings(string source, IJsonSerializer jsonSerializer, string? environment = null)
        : base(source, jsonSerializer, environment)
    {
    }
}
