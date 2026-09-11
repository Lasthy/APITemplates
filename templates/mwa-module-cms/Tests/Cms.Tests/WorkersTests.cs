using System.Reflection;
using Cms.Configuration;
using Cms.Contracts.Constants;
using Cms.Entities;
using Cms.Persistence;
using Cms.Workers;
using Kernel.Events;
using Kernel.Json;
using Kernel.Storage;
using Kernel.Testing.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cms.Tests;

public class WorkersTests : IDisposable
{
    private readonly CmsModuleDbContext _dbContext;
    private readonly FakeFileStorage _fileStorage;
    private readonly ServiceProvider _serviceProvider;
    private readonly IServiceScopeFactory _scopeFactory;

    public WorkersTests()
    {
        var services = new ServiceCollection();

        var dbName = Guid.NewGuid().ToString();
        services.AddDbContext<CmsModuleDbContext>(options =>
            options.UseInMemoryDatabase(dbName));

        var fakeStorage = new FakeFileStorage();
        services.AddSingleton<IFileStorage>(fakeStorage);
        services.AddSingleton<IEventBus, InProcessEventBus>();

        var settings = new CmsSettings(new JsonSerializer());
        services.AddSingleton(settings);

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<CmsModuleDbContext>();
        _fileStorage = fakeStorage;
        _scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _serviceProvider.Dispose();
    }

    [Fact]
    public async Task MediaCleanupWorker_DeletesOrphanedMediaBeyondGracePeriod()
    {
        var settings = _serviceProvider.GetRequiredService<CmsSettings>();
        var worker = new MediaCleanupWorker(
            _scopeFactory,
            NullLogger<MediaCleanupWorker>.Instance,
            settings);

        // Upload 3 files to fake storage
        await _fileStorage.UploadAsync("cms/media/old_orphan.jpg", new MemoryStream([1, 2, 3]));
        await _fileStorage.UploadAsync("cms/media/recent_orphan.jpg", new MemoryStream([4, 5, 6]));
        await _fileStorage.UploadAsync("cms/media/attached.jpg", new MemoryStream([7, 8, 9]));

        // 1. Orphaned, created 5 hours ago (beyond 2h grace period)
        var oldOrphan = new ContentMedia
        {
            Id = DefaultId.NewUlid(),
            ContentItemId = null,
            FileName = "old_orphan.jpg",
            StoragePath = "cms/media/old_orphan.jpg",
            MimeType = "image/jpeg",
            CreatedAt = DateTime.UtcNow.AddHours(-5)
        };

        // 2. Orphaned, created 10 minutes ago (within grace period)
        var recentOrphan = new ContentMedia
        {
            Id = DefaultId.NewUlid(),
            ContentItemId = null,
            FileName = "recent_orphan.jpg",
            StoragePath = "cms/media/recent_orphan.jpg",
            MimeType = "image/jpeg",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10)
        };

        // 3. Attached to a content item
        var attached = new ContentMedia
        {
            Id = DefaultId.NewUlid(),
            ContentItemId = DefaultId.NewUlid(),
            FileName = "attached.jpg",
            StoragePath = "cms/media/attached.jpg",
            MimeType = "image/jpeg",
            CreatedAt = DateTime.UtcNow.AddHours(-5)
        };

        _dbContext.Media.AddRange(oldOrphan, recentOrphan, attached);
        await _dbContext.SaveChangesAsync();

        // Invoke ExecuteJobAsync via reflection
        var method = typeof(MediaCleanupWorker).GetMethod(
            "ExecuteJobAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);
        var task = (Task)method.Invoke(worker, [_serviceProvider, CancellationToken.None])!;
        await task;

        // Old orphan should be deleted from DB and storage
        var dbOld = await _dbContext.Media.FindAsync(oldOrphan.Id);
        Assert.Null(dbOld);
        Assert.False(_fileStorage.ContainsFile("cms/media/old_orphan.jpg"));

        // Recent orphan should be kept
        var dbRecent = await _dbContext.Media.FindAsync(recentOrphan.Id);
        Assert.NotNull(dbRecent);
        Assert.True(_fileStorage.ContainsFile("cms/media/recent_orphan.jpg"));

        // Attached media should be kept
        var dbAttached = await _dbContext.Media.FindAsync(attached.Id);
        Assert.NotNull(dbAttached);
        Assert.True(_fileStorage.ContainsFile("cms/media/attached.jpg"));
    }

    [Fact]
    public async Task ScheduledPublishingWorker_WhenEnabled_PublishesDueItems()
    {
        var settings = _serviceProvider.GetRequiredService<CmsSettings>();
        // Mutate setting via reflection / SetValue to enable
        settings.SetValue(nameof(CmsSettings.EnableScheduledPublishing), true);

        var worker = new ScheduledPublishingWorker(
            _scopeFactory,
            NullLogger<ScheduledPublishingWorker>.Instance,
            settings);

        var dueItem = new ContentItem
        {
            Id = DefaultId.NewUlid(),
            ContentType = "Page",
            Title = "Scheduled Post",
            Slug = "scheduled-post",
            Status = ContentStatus.Draft,
            PublishAt = DateTime.UtcNow.AddMinutes(-5)
        };

        var futureItem = new ContentItem
        {
            Id = DefaultId.NewUlid(),
            ContentType = "Page",
            Title = "Future Post",
            Slug = "future-post",
            Status = ContentStatus.Draft,
            PublishAt = DateTime.UtcNow.AddHours(2)
        };

        _dbContext.ContentItems.AddRange(dueItem, futureItem);
        await _dbContext.SaveChangesAsync();

        var method = typeof(ScheduledPublishingWorker).GetMethod(
            "ExecuteJobAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);
        var task = (Task)method.Invoke(worker, [_serviceProvider, CancellationToken.None])!;
        await task;

        var refreshedDue = await _dbContext.ContentItems.FindAsync(dueItem.Id);
        var refreshedFuture = await _dbContext.ContentItems.FindAsync(futureItem.Id);

        Assert.NotNull(refreshedDue);
        Assert.Equal(ContentStatus.Published, refreshedDue.Status);
        Assert.NotNull(refreshedDue.PublishedAt);

        Assert.NotNull(refreshedFuture);
        Assert.Equal(ContentStatus.Draft, refreshedFuture.Status);
    }
}
