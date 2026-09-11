using System.Text.Json;
using Kernel.Configuration;
using Kernel.Configuration.Features;
using Kernel.Json;
using Kernel.Testing.Assertions;
using Xunit;

namespace Kernel.Tests.Configuration;

public class SettingsFeatureTests
{
    private class AppFeatureSettings : DynamicSettingsSection
    {
        public override string SectionName => "AppFeature";
        public AppFeatureSettings(IJsonSerializer jsonSerializer)
            : base("appsettings.AppFeature.json", jsonSerializer)
        {
            AutoSave = false;
        }
    }

    [Fact]
    public async Task GetSettingsHandler_WithExistingSection_ShouldReturnJson()
    {
        var serializer = new Kernel.Json.JsonSerializer();
        var dynamicSettings = new DynamicSettings(serializer);
        var section = new AppFeatureSettings(serializer);
        section.SetValue("FeatureEnabled", true);
        dynamicSettings.AddSection(section);

        var handler = new GetSettingsHandler(dynamicSettings);
        var result = await handler.HandleAsync(new GetSettings("AppFeature"));

        var json = result.ShouldBeSuccess();
        Assert.Contains("FeatureEnabled", json);
    }

    [Fact]
    public async Task GetSettingsHandler_WithNonExistentSection_ShouldReturnFailure()
    {
        var serializer = new Kernel.Json.JsonSerializer();
        var dynamicSettings = new DynamicSettings(serializer);
        var handler = new GetSettingsHandler(dynamicSettings);

        var result = await handler.HandleAsync(new GetSettings("NonExistentSection"));

        result.ShouldBeFailure<KeyNotFoundException>();
    }

    [Fact]
    public async Task UpdateSettingsHandler_ShouldUpdateValue()
    {
        var serializer = new Kernel.Json.JsonSerializer();
        var dynamicSettings = new DynamicSettings(serializer);
        var section = new AppFeatureSettings(serializer);
        dynamicSettings.AddSection(section);

        var handler = new UpdateSettingsHandler(dynamicSettings);
        using var doc = JsonDocument.Parse("\"new-value\"");

        var result = await handler.HandleAsync(new UpdateSettings("AppFeature", "Theme", doc.RootElement));

        result.ShouldBeSuccess();
        Assert.Equal("new-value", section.GetValue<string>("Theme"));
    }

    [Fact]
    public async Task UpdateSettingsHandler_WhenReadonly_ShouldReturnFailure()
    {
        var serializer = new Kernel.Json.JsonSerializer();
        var dynamicSettings = new DynamicSettings(serializer);
        var section = new AppFeatureSettings(serializer) { IsReadonly = true };
        dynamicSettings.AddSection(section);

        var handler = new UpdateSettingsHandler(dynamicSettings);
        using var doc = JsonDocument.Parse("\"new-value\"");

        var result = await handler.HandleAsync(new UpdateSettings("AppFeature", "Theme", doc.RootElement));

        result.ShouldBeFailure<InvalidOperationException>();
    }

    [Fact]
    public async Task UpdateSettingsHandler_WithMaskedValue_ShouldReject()
    {
        var serializer = new Kernel.Json.JsonSerializer();
        var dynamicSettings = new DynamicSettings(serializer);
        var section = new AppFeatureSettings(serializer);
        dynamicSettings.AddSection(section);

        var handler = new UpdateSettingsHandler(dynamicSettings);
        using var doc = JsonDocument.Parse("\"[REDACTED]\"");

        var result = await handler.HandleAsync(new UpdateSettings("AppFeature", "Secret", doc.RootElement));

        result.ShouldBeFailure<InvalidOperationException>();
    }
}
