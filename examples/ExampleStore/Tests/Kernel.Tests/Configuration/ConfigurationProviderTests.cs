using Kernel.Configuration;
using Kernel.Json;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Kernel.Tests.Configuration;

public class ConfigurationProviderTests
{
    private class PaymentSettings : DynamicSettingsSection
    {
        public override string SectionName => "Payment";
        public PaymentSettings(IJsonSerializer jsonSerializer)
            : base("appsettings.Payment.json", jsonSerializer)
        {
            AutoSave = false;
        }
    }

    [Fact]
    public void DynamicSettings_BridgesIntoIConfiguration()
    {
        var serializer = new Kernel.Json.JsonSerializer();
        var dynamicSettings = new DynamicSettings(serializer);
        var payment = new PaymentSettings(serializer);
        payment.SetValue("Provider", "Stripe");
        payment.SetValue("ApiKey", "pk_test_123");

        dynamicSettings.AddSection(payment);

        var config = new ConfigurationBuilder()
            .Add(new DynamicSettingsConfigurationSource(dynamicSettings))
            .Build();

        Assert.Equal("Stripe", config["Payment:Provider"]);
        Assert.Equal("pk_test_123", config["Payment:ApiKey"]);
    }

    [Fact]
    public void ModifyingSetting_TriggersIConfigurationReload()
    {
        var serializer = new Kernel.Json.JsonSerializer();
        var dynamicSettings = new DynamicSettings(serializer);
        var payment = new PaymentSettings(serializer);
        payment.SetValue("Provider", "Stripe");
        dynamicSettings.AddSection(payment);

        var config = new ConfigurationBuilder()
            .Add(new DynamicSettingsConfigurationSource(dynamicSettings))
            .Build();

        Assert.Equal("Stripe", config["Payment:Provider"]);

        payment.SetValue("Provider", "PayPal");

        Assert.Equal("PayPal", config["Payment:Provider"]);
    }
}
