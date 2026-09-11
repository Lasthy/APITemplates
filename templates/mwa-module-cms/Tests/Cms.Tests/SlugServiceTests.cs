using Cms.Configuration;
using Cms.Entities;
using Cms.Persistence;
using Cms.Services;
using Kernel.Json;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Cms.Tests;

public class SlugServiceTests : IDisposable
{
    private readonly CmsModuleDbContext _dbContext;
    private readonly CmsSettings _settings;
    private readonly SlugService _slugService;

    public SlugServiceTests()
    {
        var options = new DbContextOptionsBuilder<CmsModuleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new CmsModuleDbContext(options);
        _settings = new CmsSettings(new JsonSerializer());
        _slugService = new SlugService(_dbContext, _settings);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Theory]
    [InlineData("Hello World", "hello-world")]
    [InlineData("Getting Started with MWA!", "getting-started-with-mwa")]
    [InlineData("C# & ASP.NET Core: 10/10", "c-aspnet-core-10-10")]
    [InlineData("   Leading and Trailing Spaces   ", "leading-and-trailing-spaces")]
    [InlineData("Multiple    spaces---and--hyphens", "multiple-spaces-and-hyphens")]
    [InlineData("Café & Crème Brûlée", "cafe-creme-brulee")]
    [InlineData("Niño & España", "nino-espana")]
    [InlineData("München & Köln", "muenchen-koeln")]
    public void Generate_ProducesExpectedSlug(string input, string expected)
    {
        var result = _slugService.Generate(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Generate_WhenEmptyOrWhitespace_ReturnsDefaultContent()
    {
        Assert.Equal("content", _slugService.Generate(""));
        Assert.Equal("content", _slugService.Generate("   "));
        Assert.Equal("content", _slugService.Generate("???!!!###"));
    }

    [Fact]
    public void Generate_RespectsMaxSlugLength()
    {
        var longTitle = new string('a', 300);
        var slug = _slugService.Generate(longTitle);

        Assert.True(slug.Length <= _settings.MaxSlugLength);
    }

    [Fact]
    public async Task EnsureUniqueAsync_WhenNoConflict_ReturnsSameSlug()
    {
        var result = await _slugService.EnsureUniqueAsync("getting-started", "Page");
        Assert.Equal("getting-started", result);
    }

    [Fact]
    public async Task EnsureUniqueAsync_WhenConflictExists_AppendsNumericSuffix()
    {
        _dbContext.ContentItems.Add(new ContentItem
        {
            Id = DefaultId.NewUlid(),
            ContentType = "Page",
            Title = "Getting Started",
            Slug = "getting-started",
            Status = "Draft"
        });
        await _dbContext.SaveChangesAsync();

        var result1 = await _slugService.EnsureUniqueAsync("getting-started", "Page");
        Assert.Equal("getting-started-2", result1);

        _dbContext.ContentItems.Add(new ContentItem
        {
            Id = DefaultId.NewUlid(),
            ContentType = "Page",
            Title = "Getting Started 2",
            Slug = "getting-started-2",
            Status = "Draft"
        });
        await _dbContext.SaveChangesAsync();

        var result2 = await _slugService.EnsureUniqueAsync("getting-started", "Page");
        Assert.Equal("getting-started-3", result2);
    }

    [Fact]
    public async Task EnsureUniqueAsync_DifferentContentTypes_DoNotConflict()
    {
        _dbContext.ContentItems.Add(new ContentItem
        {
            Id = DefaultId.NewUlid(),
            ContentType = "Page",
            Title = "Getting Started",
            Slug = "getting-started",
            Status = "Draft"
        });
        await _dbContext.SaveChangesAsync();

        // Same slug for "Post" should NOT conflict
        var result = await _slugService.EnsureUniqueAsync("getting-started", "Post");
        Assert.Equal("getting-started", result);
    }

    [Fact]
    public async Task EnsureUniqueAsync_SoftDeletedItem_SlugCanBeReused()
    {
        _dbContext.ContentItems.Add(new ContentItem
        {
            Id = DefaultId.NewUlid(),
            ContentType = "Page",
            Title = "Old Page",
            Slug = "old-page",
            Status = "Draft",
            DeletedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        var result = await _slugService.EnsureUniqueAsync("old-page", "Page");
        Assert.Equal("old-page", result);
    }

    [Fact]
    public async Task EnsureUniqueAsync_ExcludedContentId_AllowsSameSlug()
    {
        var existingId = DefaultId.NewUlid();
        _dbContext.ContentItems.Add(new ContentItem
        {
            Id = existingId,
            ContentType = "Page",
            Title = "My Page",
            Slug = "my-page",
            Status = "Draft"
        });
        await _dbContext.SaveChangesAsync();

        // When updating existing item, its own slug is allowed
        var result = await _slugService.EnsureUniqueAsync("my-page", "Page", excludeContentId: existingId);
        Assert.Equal("my-page", result);
    }
}
