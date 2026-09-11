using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyModule.Persistence;
using Xunit;

namespace MyModule.Tests.Integration;

public class MyModuleModuleTests
{
    [Fact]
    public void ModuleName_ShouldMatchExpectedConvention()
    {
        Assert.Equal("MyModule", MyModuleModule.Name);
    }

    [Fact]
    public void Register_ShouldRegisterDbContext()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        MyModuleModule.Register(services, configuration);

        var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetService<MyModuleDbContext>();

        Assert.NotNull(dbContext);
    }
}
