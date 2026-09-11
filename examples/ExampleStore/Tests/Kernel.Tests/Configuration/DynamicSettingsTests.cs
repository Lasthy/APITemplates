using Kernel.Configuration;
using Kernel.Json;
using Kernel.Testing.Fakes;
using Xunit;

namespace Kernel.Tests.Configuration;

public class DynamicSettingsTests
{
    private class SectionA : DynamicSettingsSection
    {
        public override string SectionName => "SectionA";
        public string Title => GetValue<string>(nameof(Title)) ?? string.Empty;

        public SectionA(IJsonSerializer jsonSerializer)
            : base("appsettings.SectionA.json", jsonSerializer)
        {
            AutoSave = false;
        }
    }

    private class SectionB : DynamicSettingsSection
    {
        public override string SectionName => "SectionB";
        public int Count => GetValue<int>(nameof(Count));

        public SectionB(IJsonSerializer jsonSerializer)
            : base("appsettings.SectionB.json", jsonSerializer)
        {
            AutoSave = false;
        }
    }

    [Fact]
    public void AddSection_And_GetSection_ShouldRegisterAndRetrieve()
    {
        var serializer = new Kernel.Json.JsonSerializer();
        var settings = new DynamicSettings(serializer);
        var sectionA = new SectionA(serializer);

        settings.AddSection(sectionA);

        var retrievedByType = settings.GetSection<SectionA>();
        var retrievedByName = settings.GetSection("SectionA");

        Assert.Same(sectionA, retrievedByType);
        Assert.Same(sectionA, retrievedByName);
        Assert.Single(settings.GetSections());
    }

    [Fact]
    public void TryGetSection_ShouldReturnTrueForRegisteredSection()
    {
        var serializer = new Kernel.Json.JsonSerializer();
        var settings = new DynamicSettings(serializer);
        settings.AddSection(new SectionA(serializer));

        var found = settings.TryGetSection("SectionA", out var section);
        var notFound = settings.TryGetSection("SectionB", out var missing);

        Assert.True(found);
        Assert.NotNull(section);
        Assert.False(notFound);
        Assert.Null(missing);
    }

    [Fact]
    public void SetValue_WithEventBusWired_ShouldPublishSettingsChangedEvent()
    {
        var serializer = new Kernel.Json.JsonSerializer();
        var settings = new DynamicSettings(serializer);
        var fakeEventBus = new FakeEventBus();
        settings.SetEventBus(fakeEventBus);

        var section = new SectionA(serializer);
        settings.AddSection(section);

        section.SetValue("Title", "New Title");

        var published = fakeEventBus.GetPublishedEvents<SettingsChangedEvent>();
        Assert.Single(published);
        Assert.Equal("SectionA", published[0].SectionName);
        Assert.Equal("Title", published[0].Key);
    }

    [Fact]
    public void OnSectionAdded_ShouldFireWhenNewSectionRegistered()
    {
        var serializer = new Kernel.Json.JsonSerializer();
        var settings = new DynamicSettings(serializer);
        DynamicSettingsSection? addedSection = null;

        settings.OnSectionAdded += s => addedSection = s;

        var section = new SectionA(serializer);
        settings.AddSection(section);

        Assert.Same(section, addedSection);
    }
}
