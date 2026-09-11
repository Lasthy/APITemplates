using System.ComponentModel;
using System.Text.Json;
using Xunit;

namespace Kernel.Tests.UlidTests;

public class UlidTests
{
    [Fact]
    public void NewUlid_ShouldGenerateValidUlid()
    {
        var ulid = System.Ulid.NewUlid();

        Assert.NotEqual(System.Ulid.Empty, ulid);
        Assert.Equal(26, ulid.ToString().Length);
    }

    [Fact]
    public void Parse_And_ToString_Roundtrip()
    {
        var original = System.Ulid.NewUlid();
        var str = original.ToString();

        var parsed = System.Ulid.Parse(str);
        Assert.Equal(original, parsed);
    }

    [Fact]
    public void TryParse_ValidString_ShouldReturnTrue()
    {
        var original = System.Ulid.NewUlid();
        var success = System.Ulid.TryParse(original.ToString(), out var result);

        Assert.True(success);
        Assert.Equal(original, result);
    }

    [Fact]
    public void TryParse_InvalidString_ShouldReturnFalse()
    {
        var success = System.Ulid.TryParse("invalid-ulid-string", out var result);

        Assert.False(success);
        Assert.Equal(System.Ulid.Empty, result);
    }

    [Fact]
    public void ToByteArray_And_FromByteArray_Roundtrip()
    {
        var original = System.Ulid.NewUlid();
        var bytes = original.ToByteArray();

        Assert.Equal(16, bytes.Length);
        var restored = new System.Ulid(bytes);
        Assert.Equal(original, restored);
    }

    [Fact]
    public void Ulid_Sorting_ShouldBeMonotonicWithTime()
    {
        var first = System.Ulid.NewUlid(DateTimeOffset.UtcNow.AddMinutes(-5));
        var second = System.Ulid.NewUlid(DateTimeOffset.UtcNow);

        Assert.True(first.CompareTo(second) < 0);
        Assert.True(string.CompareOrdinal(first.ToString(), second.ToString()) < 0);
    }

    private class SampleModel
    {
        public System.Ulid Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public void KernelJsonSerializer_Serialization_ShouldWorkWithUlid()
    {
        var serializer = new Kernel.Json.JsonSerializer();
        var id = System.Ulid.NewUlid();
        var model = new SampleModel { Id = id, Name = "TestItem" };

        var json = serializer.Serialize(model);
        Assert.Contains(id.ToString(), json);

        var deserialized = serializer.Deserialize<SampleModel>(json);
        Assert.NotNull(deserialized);
        Assert.Equal(id, deserialized.Id);
        Assert.Equal("TestItem", deserialized.Name);
    }

    [Fact]
    public void TypeConverter_ShouldConvertFromStringAndToString()
    {
        var converter = TypeDescriptor.GetConverter(typeof(System.Ulid));
        Assert.True(converter.CanConvertFrom(typeof(string)));

        var original = System.Ulid.NewUlid();
        var converted = converter.ConvertFrom(original.ToString());
        Assert.Equal(original, converted);

        var str = converter.ConvertTo(original, typeof(string));
        Assert.Equal(original.ToString(), str);
    }
}
