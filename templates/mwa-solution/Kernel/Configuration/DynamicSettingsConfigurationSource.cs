using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace Kernel.Configuration;

/// <summary>
/// An <see cref="IConfigurationSource"/> that bridges <see cref="DynamicSettings"/>
/// into ASP.NET Core's <see cref="IConfiguration"/> pipeline.
/// </summary>
public class DynamicSettingsConfigurationSource : IConfigurationSource
{
    private readonly DynamicSettings _dynamicSettings;

    public DynamicSettingsConfigurationSource(DynamicSettings dynamicSettings)
    {
        _dynamicSettings = dynamicSettings;
    }

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new DynamicSettingsConfigurationProvider(_dynamicSettings);
    }
}

/// <summary>
/// A <see cref="ConfigurationProvider"/> backed by <see cref="DynamicSettings"/>.
/// Flattens all registered dynamic settings sections (and root host settings) into
/// hierarchical configuration keys and reloads whenever settings change or new sections are registered.
/// </summary>
public class DynamicSettingsConfigurationProvider : ConfigurationProvider
{
    private readonly DynamicSettings _dynamicSettings;

    public DynamicSettingsConfigurationProvider(DynamicSettings dynamicSettings)
    {
        _dynamicSettings = dynamicSettings;
        _dynamicSettings.OnSettingChanged += (section, key) => Reload();
        _dynamicSettings.OnSectionAdded += (section) => Reload();
    }

    public override void Load()
    {
        Reload();
    }

    private void Reload()
    {
        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var section in _dynamicSettings.GetSections())
        {
            var sectionName = section.SectionName;
            foreach (var (key, element) in section.MergedValues)
            {
                var prefix = string.IsNullOrEmpty(sectionName) ? key : $"{sectionName}:{key}";
                FlattenElement(data, prefix, element);
            }
        }

        Data = data;
        OnReload();
    }

    public override void Set(string key, string? value)
    {
        base.Set(key, value);

        // Try to update corresponding DynamicSettingsSection
        var parts = key.Split(':', 2);
        if (parts.Length == 2)
        {
            var sectionName = parts[0];
            var settingKey = parts[1];
            if (_dynamicSettings.TryGetSection(sectionName, out var section) && !section.IsReadonly)
            {
                ApplyValueToSection(section, settingKey, value);
                return;
            }
        }

        // Fallback to root section (empty SectionName)
        if (_dynamicSettings.TryGetSection(string.Empty, out var rootSection) && !rootSection.IsReadonly)
        {
            ApplyValueToSection(rootSection, key, value);
        }
    }

    private static void ApplyValueToSection(DynamicSettingsSection section, string key, string? value)
    {
        if (value == null)
        {
            section.SetValue(key, null!);
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(value);
            section.SetValue(key, doc.RootElement.Clone());
        }
        catch
        {
            section.SetValue(key, value);
        }
    }

    private static void FlattenElement(Dictionary<string, string?> data, string prefix, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    FlattenElement(data, $"{prefix}:{property.Name}", property.Value);
                }
                break;

            case JsonValueKind.Array:
                int index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    FlattenElement(data, $"{prefix}:{index}", item);
                    index++;
                }
                break;

            case JsonValueKind.String:
                data[prefix] = element.GetString();
                break;

            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
                data[prefix] = element.GetRawText();
                break;

            case JsonValueKind.Null:
                data[prefix] = null;
                break;

            default:
                data[prefix] = element.ToString();
                break;
        }
    }
}
