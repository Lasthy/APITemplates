using System.Text.Json;
using Cysharp.Serialization.Json;
using Identity.Entities;
using Identity.Profiles;
using Microsoft.AspNetCore.Identity;
using Xunit;

namespace Identity.Tests;

public class ProfileCreationContextTests
{
    private record SampleCompanionDto(string OrgName, int EmployeeCount);

    private enum UserCategory
    {
        Standard,
        Premium,
        Enterprise
    }

    private static ProfileCreationContext CreateTestContext(
        IReadOnlyDictionary<string, object>? additionalData = null)
    {
        var user = new ApplicationUser
        {
            Id = Ulid.NewUlid(),
            Email = "test@example.com",
            UserName = "testuser",
            ProfileName = "Test"
        };

        // Note: UserManager is required by ProfileCreationContext init, but not used for AdditionalData helpers
        return new ProfileCreationContext
        {
            User = user,
            UserManager = null!,
            Services = null!,
            AdditionalData = additionalData
        };
    }

    [Fact]
    public void HasAdditionalData_WorksCaseInsensitive_AndHandlesNull()
    {
        var contextWithNull = CreateTestContext(null);
        Assert.False(contextWithNull.HasAdditionalData("companyName"));

        var contextWithData = CreateTestContext(new Dictionary<string, object>
        {
            ["companyName"] = "Acme Corp"
        });

        Assert.True(contextWithData.HasAdditionalData("companyName"));
        Assert.True(contextWithData.HasAdditionalData("CompanyName"));
        Assert.True(contextWithData.HasAdditionalData("COMPANYNAME"));
        Assert.False(contextWithData.HasAdditionalData("nonExistent"));
    }

    [Fact]
    public void GetAdditionalData_PrimitiveTypes_DirectObjectCasting()
    {
        var expectedGuid = Guid.NewGuid();
        var expectedUlid = Ulid.NewUlid();

        var context = CreateTestContext(new Dictionary<string, object>
        {
            ["orgName"] = "Acme Corp",
            ["age"] = 35,
            ["isActive"] = true,
            ["budget"] = 12500.50m,
            ["rating"] = 4.8,
            ["orgId"] = expectedGuid,
            ["entityId"] = expectedUlid,
            ["category"] = UserCategory.Enterprise
        });

        Assert.Equal("Acme Corp", context.GetAdditionalData<string>("orgName"));
        Assert.Equal("Acme Corp", context.GetAdditionalData<string>("OrgName")); // case-insensitive
        Assert.Equal(35, context.GetAdditionalData<int>("age"));
        Assert.True(context.GetAdditionalData<bool>("isActive"));
        Assert.Equal(12500.50m, context.GetAdditionalData<decimal>("budget"));
        Assert.Equal(4.8, context.GetAdditionalData<double>("rating"));
        Assert.Equal(expectedGuid, context.GetAdditionalData<Guid>("orgId"));
        Assert.Equal(expectedUlid, context.GetAdditionalData<Ulid>("entityId"));
        Assert.Equal(UserCategory.Enterprise, context.GetAdditionalData<UserCategory>("category"));

        // Aliases
        Assert.Equal("Acme Corp", context.GetData<string>("orgName"));
        Assert.Equal("Acme Corp", context.Get<string>("orgName"));
    }

    [Fact]
    public void GetAdditionalData_JsonElement_PrimitivesAndTypes()
    {
        var expectedGuid = Guid.NewGuid();
        var expectedUlid = Ulid.NewUlid();

        var json = $$"""
        {
            "orgName": "Global Corp",
            "age": 42,
            "isActive": true,
            "budget": 99999.99,
            "orgId": "{{expectedGuid}}",
            "entityId": "{{expectedUlid}}",
            "category": "Premium"
        }
        """;

        var jsonDoc = JsonDocument.Parse(json);
        var dict = new Dictionary<string, object>();
        foreach (var prop in jsonDoc.RootElement.EnumerateObject())
        {
            dict[prop.Name] = prop.Value;
        }

        var context = CreateTestContext(dict);

        Assert.Equal("Global Corp", context.GetAdditionalData<string>("orgName"));
        Assert.Equal("Global Corp", context.GetAdditionalData<string>("OrgName"));
        Assert.Equal(42, context.GetAdditionalData<int>("age"));
        Assert.True(context.GetAdditionalData<bool>("isActive"));
        Assert.Equal(99999.99m, context.GetAdditionalData<decimal>("budget"));
        Assert.Equal(expectedGuid, context.GetAdditionalData<Guid>("orgId"));
        Assert.Equal(expectedUlid, context.GetAdditionalData<Ulid>("entityId"));
        Assert.Equal(UserCategory.Premium, context.GetAdditionalData<UserCategory>("category"));
    }

    [Fact]
    public void GetAdditionalData_ComplexDto_FromJsonElement()
    {
        var json = """
        {
            "companion": {
                "orgName": "Companion Organization",
                "employeeCount": 150
            }
        }
        """;

        var jsonDoc = JsonDocument.Parse(json);
        var dict = new Dictionary<string, object>
        {
            ["companion"] = jsonDoc.RootElement.GetProperty("companion")
        };

        var context = CreateTestContext(dict);

        var companion = context.GetAdditionalData<SampleCompanionDto>("companion");
        Assert.NotNull(companion);
        Assert.Equal("Companion Organization", companion.OrgName);
        Assert.Equal(150, companion.EmployeeCount);
    }

    [Fact]
    public void TryGetAdditionalData_MissingOrInvalid_ReturnsFalse()
    {
        var context = CreateTestContext(new Dictionary<string, object>
        {
            ["notAnInt"] = "not_a_number"
        });

        Assert.False(context.TryGetAdditionalData<int>("notAnInt", out var intVal));
        Assert.Equal(0, intVal);

        Assert.False(context.TryGetAdditionalData<string>("missingKey", out var strVal));
        Assert.Null(strVal);
    }

    [Fact]
    public void Items_CanStoreTransientHookState()
    {
        var context = CreateTestContext();
        context.Items["CompanionCreated"] = true;
        context.Items["CompanionId"] = 12345;

        Assert.True(context.Items.ContainsKey("companioncreated")); // case-insensitive
        Assert.Equal(true, context.Items["CompanionCreated"]);
        Assert.Equal(12345, context.Items["companionId"]);
    }

    [Fact]
    public void Indexer_AccessesRawAdditionalDataCaseInsensitively()
    {
        var context = CreateTestContext(new Dictionary<string, object>
        {
            ["myKey"] = "myValue"
        });

        Assert.Equal("myValue", context["myKey"]);
        Assert.Equal("myValue", context["MYKEY"]);
        Assert.Null(context["nonExistent"]);
    }
}
