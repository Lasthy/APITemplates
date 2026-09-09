using Cms.Contracts.Constants;
using Cms.Contracts.Requests;
using Cms.Entities;
using Cms.Features;
using Cms.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Cms.Tests;

public class CrossModuleCmsTests : IDisposable
{
    private readonly CmsModuleDbContext _dbContext;

    public CrossModuleCmsTests()
    {
        var options = new DbContextOptionsBuilder<CmsModuleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new CmsModuleDbContext(options);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task GetContentBySlugHandler_ReturnsContentDetail()
    {
        var item = new ContentItem
        {
            Id = DefaultId.NewUlid(),
            ContentType = "Page",
            Title = "Documentation",
            Slug = "documentation",
            Body = "# Docs",
            Status = ContentStatus.Published
        };
        _dbContext.ContentItems.Add(item);
        await _dbContext.SaveChangesAsync();

        var handler = new GetContentBySlugHandler(_dbContext);
        var result = await handler.HandleAsync(new GetContentBySlug("Page", "documentation"));

        Assert.True(result.IsSuccess);
        Assert.Equal(item.Id, result.Value.Id);
        Assert.Equal("Documentation", result.Value.Title);
        Assert.Equal("# Docs", result.Value.Body);
    }

    [Fact]
    public async Task GetContentSummaryHandler_ReturnsContentSummary()
    {
        var item = new ContentItem
        {
            Id = DefaultId.NewUlid(),
            ContentType = "Post",
            Title = "Product Announcement",
            Slug = "product-announcement",
            Excerpt = "Big news today!",
            Status = ContentStatus.Published
        };
        _dbContext.ContentItems.Add(item);
        await _dbContext.SaveChangesAsync();

        var handler = new GetContentSummaryHandler(_dbContext);
        var result = await handler.HandleAsync(new GetContentSummary(item.Id));

        Assert.True(result.IsSuccess);
        Assert.Equal(item.Id, result.Value.Id);
        Assert.Equal("Product Announcement", result.Value.Title);
        Assert.Equal("Big news today!", result.Value.Excerpt);
    }

    [Fact]
    public async Task CrossModuleHandlers_WhenNotFound_ReturnFailure()
    {
        var slugHandler = new GetContentBySlugHandler(_dbContext);
        var summaryHandler = new GetContentSummaryHandler(_dbContext);

        var slugResult = await slugHandler.HandleAsync(new GetContentBySlug("Page", "does-not-exist"));
        Assert.True(slugResult.IsFailure);

        var summaryResult = await summaryHandler.HandleAsync(new GetContentSummary(DefaultId.NewUlid()));
        Assert.True(summaryResult.IsFailure);
    }
}
