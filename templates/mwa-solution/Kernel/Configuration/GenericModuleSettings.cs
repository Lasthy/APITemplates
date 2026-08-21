using Kernel.Json;

namespace Kernel.Configuration;

/// <summary>
/// A generic <see cref="DynamicSettingsSection"/> for modules that don't define
/// a custom subclass. The section name is set dynamically to the module name.
/// <para>
/// When a module has a <c>settings.{ModuleName}.json</c> file but no custom
/// <see cref="DynamicSettingsSection"/> subclass, this class is used as a fallback
/// so the module's settings are still accessible via <see cref="DynamicSettings"/>.
/// </para>
/// </summary>
internal sealed class GenericModuleSettings : DynamicSettingsSection
{
    private readonly string _sectionName;

    /// <inheritdoc />
    public override string SectionName => _sectionName;

    public GenericModuleSettings(string sectionName, string source, IJsonSerializer jsonSerializer, string? environment = null)
        : base(source, jsonSerializer, environment)
    {
        _sectionName = sectionName;
    }
}
