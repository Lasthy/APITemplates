using Cms.Configuration;
using Cms.ContentTypes;
using Cms.ContentTypes.BuiltIn;
using Cms.Contracts.Constants;
using Cms.Contracts.Events;
using Cms.Entities;
using Cms.Persistence;
using Cms.Services;
using Kernel.Common;
using Kernel.Context;
using Kernel.Events;
using Kernel.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cms.Tests;

public class ContentServiceTests : IDisposable
{
    private readonly CmsModuleDbContext _dbContext;
    private readonly ContentTypeRegistry _registry;
    private readonly ContentService _contentService;
    private readonly TestEventBus _eventBus;
    private readonly TestScopeContext _scopeContext;

    public ContentServiceTests()
    {
        var options = new DbContextOptionsBuilder<CmsModuleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new CmsModuleDbContext(options);
        _registry = new ContentTypeRegistry();
        _registry.Register<PageContentType>();
        _registry.Register<PostContentType>();
        _registry.Register<FaqContentType>();

        var settings = new CmsSettings(new JsonSerializer());
        var slugService = new SlugService(_dbContext, settings);
        _eventBus = new TestEventBus();
        _scopeContext = new TestScopeContext();
        var logger = NullLogger<ContentService>.Instance;

        _contentService = new ContentService(
            _dbContext,
            _registry,
            slugService,
            _eventBus,
            _scopeContext,
            settings,
            logger);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private class TestEventBus : IEventBus
    {
        public List<IEvent> PublishedEvents { get; } = [];

        public Task PublishAsync<T>(T @event, CancellationToken ct = default) where T : IEvent
        {
            PublishedEvents.Add(@event);
            return Task.CompletedTask;
        }
    }

    private class TestScopeContext : IScopeContext
    {
        public DefaultId? UserId { get; set; } = DefaultId.NewUlid();
        public bool IsAuthenticated => true;
        public IReadOnlyList<string> Roles => ["Admin"];
        public IReadOnlyDictionary<string, string> Claims => new Dictionary<string, string>();
        public string? GetClaim(string type) => null;
        public IReadOnlyList<string> GetClaimValues(string type) => [];
        public bool IsInRole(string role) => true;
        public bool IsSystem => false;
        public string? CorrelationId => null;
    }

    [Fact]
    public async Task CreateAsync_ValidInput_CreatesItemAndInitialRevision()
    {
        var result = await _contentService.CreateAsync(
            contentType: "Page",
            title: "About Us",
            body: "# About Us\nWelcome to our store.",
            excerpt: "Learn more about our company.",
            fieldsJson: null,
            authorId: _scopeContext.UserId!.Value);

        Assert.True(result.IsSuccess);
        var item = result.Value;
        Assert.Equal("Page", item.ContentType);
        Assert.Equal("About Us", item.Title);
        Assert.Equal("about-us", item.Slug);
        Assert.Equal(ContentStatus.Draft, item.Status);
        Assert.Equal(1, item.RevisionNumber);

        // Verify revision #1 created
        var revision = await _dbContext.Revisions
            .FirstOrDefaultAsync(r => r.ContentItemId == item.Id && r.RevisionNumber == 1);
        Assert.NotNull(revision);
        Assert.Equal("About Us", revision.Title);
        Assert.Equal(item.Body, revision.Body);

        // Verify event published
        var createdEvent = _eventBus.PublishedEvents.OfType<ContentCreatedEvent>().FirstOrDefault();
        Assert.NotNull(createdEvent);
        Assert.Equal(item.Id, createdEvent.ContentId);
        Assert.Equal("about-us", createdEvent.Slug);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesContentAndCreatesNewRevision()
    {
        var createResult = await _contentService.CreateAsync(
            "Page", "My Page", "Initial body", null, null, _scopeContext.UserId!.Value);
        Assert.True(createResult.IsSuccess);
        var item = createResult.Value;

        var updateResult = await _contentService.UpdateAsync(
            item.Id,
            title: "My Page Updated",
            body: "New updated body",
            excerpt: "New excerpt",
            fieldsJson: null,
            metadataJson: """{"seoTitle": "Updated"}""",
            changeNote: "Updated copy");

        Assert.True(updateResult.IsSuccess);

        var updated = await _dbContext.ContentItems.FindAsync(item.Id);
        Assert.NotNull(updated);
        Assert.Equal("My Page Updated", updated.Title);
        Assert.Equal("my-page-updated", updated.Slug);
        Assert.Equal("New updated body", updated.Body);
        Assert.Equal(2, updated.RevisionNumber);

        // Verify revisions 1 and 2 exist
        var rev1 = await _dbContext.Revisions.FirstOrDefaultAsync(r => r.ContentItemId == item.Id && r.RevisionNumber == 1);
        var rev2 = await _dbContext.Revisions.FirstOrDefaultAsync(r => r.ContentItemId == item.Id && r.RevisionNumber == 2);
        Assert.NotNull(rev1);
        Assert.NotNull(rev2);
        Assert.Equal("My Page", rev1.Title);
        Assert.Equal("My Page Updated", rev2.Title);
        Assert.Equal("Updated copy", rev2.ChangeNote);

        // Verify event published
        var updateEvent = _eventBus.PublishedEvents.OfType<ContentUpdatedEvent>().FirstOrDefault();
        Assert.NotNull(updateEvent);
        Assert.Equal(item.Id, updateEvent.ContentId);
        Assert.Equal(2, updateEvent.RevisionNumber);
    }

    [Fact]
    public async Task Publish_Unpublish_Archive_TransitionsStatusCorrectly()
    {
        var createResult = await _contentService.CreateAsync(
            "Page", "Status Test", "Body", null, null, _scopeContext.UserId!.Value);
        var id = createResult.Value.Id;

        // 1. Publish
        var publishResult = await _contentService.PublishAsync(id);
        Assert.True(publishResult.IsSuccess);
        var publishedItem = await _dbContext.ContentItems.FindAsync(id);
        Assert.Equal(ContentStatus.Published, publishedItem!.Status);
        Assert.NotNull(publishedItem.PublishedAt);
        Assert.Contains(_eventBus.PublishedEvents, e => e is ContentPublishedEvent);

        // 2. Unpublish
        var unpublishResult = await _contentService.UnpublishAsync(id);
        Assert.True(unpublishResult.IsSuccess);
        var unpublishedItem = await _dbContext.ContentItems.FindAsync(id);
        Assert.Equal(ContentStatus.Draft, unpublishedItem!.Status);
        Assert.Contains(_eventBus.PublishedEvents, e => e is ContentUnpublishedEvent);

        // 3. Archive
        var archiveResult = await _contentService.ArchiveAsync(id);
        Assert.True(archiveResult.IsSuccess);
        var archivedItem = await _dbContext.ContentItems.FindAsync(id);
        Assert.Equal(ContentStatus.Archived, archivedItem!.Status);
        Assert.Contains(_eventBus.PublishedEvents, e => e is ContentArchivedEvent);
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletesContentItem()
    {
        var createResult = await _contentService.CreateAsync(
            "Page", "Delete Test", "Body", null, null, _scopeContext.UserId!.Value);
        var id = createResult.Value.Id;

        var deleteResult = await _contentService.DeleteAsync(id);
        Assert.True(deleteResult.IsSuccess);

        // Normal query should not find it due to HasQueryFilter
        var item = await _dbContext.ContentItems.FirstOrDefaultAsync(ci => ci.Id == id);
        Assert.Null(item);

        // IgnoreQueryFilters should find it
        var deletedItem = await _dbContext.ContentItems.IgnoreQueryFilters().FirstOrDefaultAsync(ci => ci.Id == id);
        Assert.NotNull(deletedItem);
        Assert.NotNull(deletedItem.DeletedAt);
        Assert.Equal(_scopeContext.UserId, deletedItem.DeletedBy);

        Assert.Contains(_eventBus.PublishedEvents, e => e is ContentDeletedEvent);
    }

    [Fact]
    public async Task RestoreRevisionAsync_RestoresHistoricalState()
    {
        var createResult = await _contentService.CreateAsync(
            "Page", "Version 1", "Original Body", null, null, _scopeContext.UserId!.Value);
        var id = createResult.Value.Id;

        // Update to v2
        await _contentService.UpdateAsync(id, "Version 2", "Changed Body", null, null, null, "v2 update");

        // Restore revision 1
        var restoreResult = await _contentService.RestoreRevisionAsync(id, 1);
        Assert.True(restoreResult.IsSuccess);

        var restored = await _dbContext.ContentItems.FindAsync(id);
        Assert.NotNull(restored);
        Assert.Equal("Version 1", restored.Title);
        Assert.Equal("Original Body", restored.Body);
        Assert.Equal(3, restored.RevisionNumber); // Restoration increments revision number

        var rev3 = await _dbContext.Revisions.FirstOrDefaultAsync(r => r.ContentItemId == id && r.RevisionNumber == 3);
        Assert.NotNull(rev3);
        Assert.Contains("Restored from revision 1", rev3.ChangeNote);
    }

    [Fact]
    public async Task SetTagsAsync_ReplacesTagsSuccessfully()
    {
        var tag1 = new ContentTag { Id = DefaultId.NewUlid(), Name = "C#", Slug = "csharp" };
        var tag2 = new ContentTag { Id = DefaultId.NewUlid(), Name = "ASP.NET", Slug = "aspnet" };
        var tag3 = new ContentTag { Id = DefaultId.NewUlid(), Name = "Architecture", Slug = "architecture" };
        _dbContext.Tags.AddRange(tag1, tag2, tag3);
        await _dbContext.SaveChangesAsync();

        var createResult = await _contentService.CreateAsync(
            "Page", "Tagged Page", "Body", null, null, _scopeContext.UserId!.Value, [tag1.Id]);
        var id = createResult.Value.Id;

        var setTagsResult = await _contentService.SetTagsAsync(id, [tag2.Id, tag3.Id]);
        Assert.True(setTagsResult.IsSuccess);

        var assignedTags = await _dbContext.ContentItemTags
            .Where(cit => cit.ContentItemId == id)
            .Select(cit => cit.ContentTagId)
            .ToListAsync();

        Assert.Equal(2, assignedTags.Count);
        Assert.Contains(tag2.Id, assignedTags);
        Assert.Contains(tag3.Id, assignedTags);
        Assert.DoesNotContain(tag1.Id, assignedTags);
    }
}
