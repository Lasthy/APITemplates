using Kernel.Configuration;
using Kernel.Json;

namespace Cms.Configuration;

/// <summary>
/// Dynamic settings section for the CMS module, loaded from <c>appsettings.Cms.json</c>
/// with optional environment overlay <c>appsettings.Cms.{Environment}.json</c>.
/// </summary>
public class CmsSettings : DynamicSettingsSection
{
    public override string SectionName => "Cms";

    public string DefaultStatus =>
        TryGetValue<string>(nameof(DefaultStatus), out var val) && !string.IsNullOrWhiteSpace(val) ? val : "Draft";

    public string DefaultBodyFormat =>
        TryGetValue<string>(nameof(DefaultBodyFormat), out var val) && !string.IsNullOrWhiteSpace(val) ? val : "markdown";

    public int MaxBodyLength =>
        TryGetValue<int>(nameof(MaxBodyLength), out var val) && val > 0 ? val : 500000;

    public int MaxExcerptLength =>
        TryGetValue<int>(nameof(MaxExcerptLength), out var val) && val > 0 ? val : 1000;

    public int MaxTitleLength =>
        TryGetValue<int>(nameof(MaxTitleLength), out var val) && val > 0 ? val : 300;

    public int MaxSlugLength =>
        TryGetValue<int>(nameof(MaxSlugLength), out var val) && val > 0 ? val : 200;

    public int MaxFieldsJsonLength =>
        TryGetValue<int>(nameof(MaxFieldsJsonLength), out var val) && val > 0 ? val : 50000;

    public int MaxMetadataJsonLength =>
        TryGetValue<int>(nameof(MaxMetadataJsonLength), out var val) && val > 0 ? val : 10000;

    public int MaxTagsPerContent =>
        TryGetValue<int>(nameof(MaxTagsPerContent), out var val) && val > 0 ? val : 20;

    public int MaxMediaPerContent =>
        TryGetValue<int>(nameof(MaxMediaPerContent), out var val) && val > 0 ? val : 50;

    public int MaxMediaFileSizeMb =>
        TryGetValue<int>(nameof(MaxMediaFileSizeMb), out var val) && val > 0 ? val : 25;

    public IReadOnlyList<string> AllowedMediaMimeTypes =>
        TryGetValue<List<string>>(nameof(AllowedMediaMimeTypes), out var val) && val != null && val.Count > 0
            ? val
            : ["image/*", "application/pdf", "video/mp4"];

    public string MediaStoragePath =>
        TryGetValue<string>(nameof(MediaStoragePath), out var val) && !string.IsNullOrWhiteSpace(val) ? val : "cms/media";

    public int OrphanedMediaCleanupIntervalHours =>
        TryGetValue<int>(nameof(OrphanedMediaCleanupIntervalHours), out var val) && val > 0 ? val : 24;

    public int OrphanedMediaGracePeriodHours =>
        TryGetValue<int>(nameof(OrphanedMediaGracePeriodHours), out var val) && val > 0 ? val : 2;

    public bool EnableScheduledPublishing =>
        TryGetValue<bool>(nameof(EnableScheduledPublishing), out var val) && val;

    public int ScheduledPublishingIntervalSeconds =>
        TryGetValue<int>(nameof(ScheduledPublishingIntervalSeconds), out var val) && val > 0 ? val : 60;

    public CmsSettings(IJsonSerializer jsonSerializer, string? environment = null)
        : base("appsettings.Cms.json", jsonSerializer, environment)
    {
    }

    public CmsSettings(string source, IJsonSerializer jsonSerializer, string? environment = null)
        : base(source, jsonSerializer, environment)
    {
    }
}
