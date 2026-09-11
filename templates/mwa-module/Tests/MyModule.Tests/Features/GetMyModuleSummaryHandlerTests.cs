using Microsoft.Extensions.Logging.Abstractions;
using MyModule.Contracts.Requests;
using MyModule.Features;
using Xunit;

namespace MyModule.Tests.Features;

public class GetMyModuleSummaryHandlerTests
{
    [Fact]
    public async Task HandleAsync_WithValidId_ShouldReturnSummaryDto()
    {
        // Arrange
        var handler = new GetMyModuleSummaryHandler(NullLogger<GetMyModuleSummaryHandler>.Instance);
        var targetId = DefaultId.NewUlid();
        var request = new GetMyModuleSummary(targetId);

        // Act
        var result = await handler.HandleAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(targetId, result.Value.Id);
        Assert.Equal("Sample Resource", result.Value.Name);
        Assert.True(result.Value.IsActive);
    }
}
