using System.Text.Json;
using Kernel.Configuration;
using Kernel.Json;
using Xunit;

namespace Kernel.Tests.Configuration;

public class DynamicSettingsSectionTests
{
    private class TestSettingsSection : DynamicSettingsSection
    {
        public override string SectionName => "TestSection";

        public string Host => GetValue<string>(nameof(Host)) ?? "localhost";
        public int Port => GetValue<int>(nameof(Port));
        public bool Enabled => GetValue<bool>(nameof(Enabled));

        public TestSettingsSection(IJsonSerializer jsonSerializer, string? environment = null)
            : base("appsettings.TestSection.json", jsonSerializer, environment)
        {
            AutoSave = false; // Disable disk writes in tests
        }
    }

    [Fact]
    public void SetValue_And_GetValue_ShouldStoreAndRetrieveValues()
    {
        var serializer = new Kernel.Json.JsonSerializer();
        var section = new TestSettingsSection(serializer);

        section.SetValue("Host", "api.example.com");
        section.SetValue("Port", 8080);
        section.SetValue("Enabled", true);

        Assert.Equal("api.example.com", section.Host);
        Assert.Equal(8080, section.Port);
        Assert.True(section.Enabled);
    }

    [Fact]
    public void TryGetValue_ExistingKey_ShouldReturnTrueAndValue()
    {
        var serializer = new Kernel.Json.JsonSerializer();
        var section = new TestSettingsSection(serializer);
        section.SetValue("ApiKey", "secret-key-123");

        var found = section.TryGetValue<string>("ApiKey", out var value);

        Assert.True(found);
        Assert.Equal("secret-key-123", value);
    }

    [Fact]
    public void TryGetValue_MissingKey_ShouldReturnFalse()
    {
        var serializer = new Kernel.Json.JsonSerializer();
        var section = new TestSettingsSection(serializer);

        var found = section.TryGetValue<string>("NonExistentKey", out var value);

        Assert.False(found);
        Assert.Null(value);
    }

    [Fact]
    public void SetValue_WhenReadOnly_ShouldIgnoreChanges()
    {
        var serializer = new Kernel.Json.JsonSerializer();
        var section = new TestSettingsSection(serializer);
        section.SetValue("Port", 5000);

        section.IsReadonly = true;
        section.SetValue("Port", 9000);

        Assert.Equal(5000, section.Port);
    }

    [Fact]
    public void SetValue_ShouldRaiseOnSettingsChangedEvent()
    {
        var serializer = new Kernel.Json.JsonSerializer();
        var section = new TestSettingsSection(serializer);

        DynamicSettingsSection? eventSender = null;
        string? changedKey = null;

        section.OnSettingsChanged += (s, key) =>
        {
            eventSender = s;
            changedKey = key;
        };

        section.SetValue("Host", "custom-host");

        Assert.Same(section, eventSender);
        Assert.Equal("Host", changedKey);
    }

    [Fact]
    public void HasPendingChanges_ShouldTrackChangesWhenAutoSaveIsFalse()
    {
        var serializer = new Kernel.Json.JsonSerializer();
        var section = new TestSettingsSection(serializer) { AutoSave = false };

        Assert.False(section.HasPendingChanges);

        section.SetValue("Host", "new-host");

        Assert.True(section.HasPendingChanges);
    }
}
