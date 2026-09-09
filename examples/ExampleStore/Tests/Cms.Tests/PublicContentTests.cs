using Cms.Contracts.Constants;
using Cms.Entities;
using Cms.Features.Public;
using Cms.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Cms.Tests;

public class PublicContentTests : IDisposable
{
    private readonly CmsModuleDbContext _dbContext;

    public PublicContentTests()
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
    public async Task GetPublishedContents_OnlyReturnsPublishedItems()
    {
        var publishedItem = new ContentItem
        {
            Id = DefaultId.NewUlid(),
            ContentType = "Page",
            Title = "Published Page",
            Slug = "published-page",
            Status = ContentStatus.Published,
            PublishedAt = DateTime.UtcNow
        };

        var draftItem = new ContentItem
        {
            Id = DefaultId.NewUlid(),
            ContentType = "Page",
            Title = "Draft Page",
            Slug = "draft-page",
            Status = ContentStatus.Draft
        };

        var archivedItem = new ContentItem
        {
            Id = DefaultId.NewUlid(),
            ContentType = "Page",
            Title = "Archived Page",
            Slug = "archived-page",
            Status = ContentStatus.Archived
        };

        _dbContext.ContentItems.AddRange(publishedItem, draftItem, archivedItem);
        await _dbContext.SaveChangesAsync();

        var handler = new GetPublishedContentsHandler(_dbContext);
        var result = await handler.HandleAsync(new GetPublishedContents());

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
        Assert.Equal("published-page", result.Value.Items.First().Slug);
    }

    [Fact]
    public async Task GetPublishedContentBySlug_ReturnsPublishedItem_NotFoundForDraft()
    {
        var published = new ContentItem
        {
            Id = DefaultId.NewUlid(),
            ContentType = "Page",
            Title = "Live Page",
            Slug = "live-page",
            Status = ContentStatus.Published
        };

        var draft = new ContentItem
        {
            Id = DefaultId.NewUlid(),
            ContentType = "Page",
            Title = "Secret Draft",
            Slug = "secret-draft",
            Status = ContentStatus.Draft
        };

        _dbContext.ContentItems.AddRange(published, draft);
        await _dbContext.SaveChangesAsync();

        var handler = new GetPublishedContentBySlugHandler(_dbContext);

        var pubResult = await handler.HandleAsync(new GetPublishedContentBySlug("Page", "live-page"));
        Assert.True(pubResult.IsSuccess);
        Assert.Equal("Live Page", pubResult.Value.Title);

        var draftResult = await handler.HandleAsync(new GetPublishedContentBySlug("Page", "secret-draft"));
        Assert.True(draftResult.IsFailure);
    }

    [Fact]
    public async Task GetPublicTags_ReturnsAccuratePublishedContentCounts()
    {
        var tag1 = new ContentTag { Id = DefaultId.NewUlid(), Name = "News", Slug = "news" };
        var tag2 = new ContentTag { Id = DefaultId.NewUlid(), Name = "Tech", Slug = "tech" };
        _dbContext.Tags.AddRange(tag1, tag2);

        var pub1 = new ContentItem { Id = DefaultId.NewUlid(), ContentType = "Post", Title = "Post 1", Slug = "post-1", Status = ContentStatus.Published };
        var pub2 = new ContentItem { Id = DefaultId.NewUlid(), ContentType = "Post", Title = "Post 2", Slug = "post-2", Status = ContentStatus.Published };
        var draft = new ContentItem { Id = DefaultId.NewUlid(), ContentType = "Post", Title = "Post 3", Slug = "post-3", Status = ContentStatus.Draft };
        _dbContext.ContentItems.AddRange(pub1, pub2, draft);

        // tag1 has 2 published items + 1 draft item
        _dbContext.ContentItemTags.Add(new ContentItemTag { ContentItemId = pub1.Id, ContentTagId = tag1.Id });
        _dbContext.ContentItemTags.Add(new ContentItemTag { ContentItemId = pub2.Id, ContentTagId = tag1.Id });
        _dbContext.ContentItemTags.Add(new ContentItemTag { ContentItemId = draft.Id, ContentTagId = tag1.Id });

        // tag2 has 0 published items
        _dbContext.ContentItemTags.Add(new ContentItemTag { ContentItemId = draft.Id, ContentTagId = tag2.Id });

        await _dbContext.SaveChangesAsync();

        var handler = new GetPublicTagsHandler(_dbContext);
        var result = await handler.HandleAsync(new GetPublicTags());

        Assert.True(result.IsSuccess);
        var newsTag = result.Value.FirstOrDefault(t => t.Slug == "news");
        var techTag = result.Value.FirstOrDefault(t => t.Slug == "tech");

        Assert.NotNull(newsTag);
        Assert.NotNull(techTag);
        Assert.Equal(2, newsTag.ContentCount); // only published
        Assert.Equal(0, techTag.ContentCount);
    }
}
