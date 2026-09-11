using System.Text;
using Cms.Configuration;
using Cms.ContentTypes;
using Cms.ContentTypes.BuiltIn;
using Cms.Contracts.Constants;
using Cms.Contracts.Events;
using Cms.Entities;
using Cms.Persistence;
using Cms.Services;
using Kernel.Events;
using Kernel.Json;
using Kernel.Testing.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cms.Tests;

public class ContentMediaServiceTests : IDisposable
{
    private readonly CmsModuleDbContext _dbContext;
    private readonly FakeFileStorage _fileStorage;
    private readonly ContentTypeRegistry _registry;
    private readonly TestEventBus _eventBus;
    private readonly ContentMediaService _mediaService;

    public ContentMediaServiceTests()
    {
        var options = new DbContextOptionsBuilder<CmsModuleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new CmsModuleDbContext(options);
        _fileStorage = new FakeFileStorage();
        _registry = new ContentTypeRegistry();
        _registry.Register<PageContentType>();
        _registry.Register<PostContentType>();
        _registry.Register<FaqContentType>();

        _eventBus = new TestEventBus();
        var settings = new CmsSettings(new JsonSerializer());
        var logger = NullLogger<ContentMediaService>.Instance;

        _mediaService = new ContentMediaService(
            _dbContext,
            _fileStorage,
            _registry,
            _eventBus,
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

    [Fact]
    public async Task UploadAsync_ValidImage_UploadsAndPersists()
    {
        var content = "fake image content"u8.ToArray();
        using var stream = new MemoryStream(content);

        var result = await _mediaService.UploadAsync(
            contentItemId: null,
            fileStream: stream,
            fileName: "banner.jpg",
            mimeType: "image/jpeg",
            altText: "Store banner");

        Assert.True(result.IsSuccess);
        var media = result.Value;
        Assert.Equal("banner.jpg", media.FileName);
        Assert.Equal("image/jpeg", media.MimeType);
        Assert.Equal("Store banner", media.AltText);
        Assert.Equal(content.Length, media.SizeBytes);
        Assert.NotNull(media.StoragePath);

        // Verify file stored in fake storage
        Assert.True(_fileStorage.ContainsFile(media.StoragePath));

        // Verify event published
        var uploadedEvent = _eventBus.PublishedEvents.OfType<MediaUploadedEvent>().FirstOrDefault();
        Assert.NotNull(uploadedEvent);
        Assert.Equal(media.Id, uploadedEvent.MediaId);
    }

    [Fact]
    public async Task UploadAsync_DisallowedMimeType_Fails()
    {
        var content = "malicious payload"u8.ToArray();
        using var stream = new MemoryStream(content);

        var result = await _mediaService.UploadAsync(
            contentItemId: null,
            fileStream: stream,
            fileName: "script.exe",
            mimeType: "application/x-msdownload");

        Assert.True(result.IsFailure);
        Assert.Contains("not allowed", result.Messages.First());
    }

    [Fact]
    public async Task UploadAsync_WhenContentTypeDoesNotSupportMedia_Fails()
    {
        // Faq has SupportsMedia = false
        var faqItem = new ContentItem
        {
            Id = DefaultId.NewUlid(),
            ContentType = "Faq",
            Title = "FAQ 1",
            Slug = "faq-1",
            Status = ContentStatus.Draft
        };
        _dbContext.ContentItems.Add(faqItem);
        await _dbContext.SaveChangesAsync();

        using var stream = new MemoryStream("image"u8.ToArray());
        var result = await _mediaService.UploadAsync(
            contentItemId: faqItem.Id,
            fileStream: stream,
            fileName: "faq-image.png",
            mimeType: "image/png");

        Assert.True(result.IsFailure);
        Assert.Contains("does not support media", result.Messages.First());
    }

    [Fact]
    public async Task DeleteAsync_DeletesFromStorageAndDatabase()
    {
        using var stream = new MemoryStream("image data"u8.ToArray());
        var uploadResult = await _mediaService.UploadAsync(null, stream, "test.png", "image/png");
        Assert.True(uploadResult.IsSuccess);
        var mediaId = uploadResult.Value.Id;
        var storagePath = uploadResult.Value.StoragePath;

        var deleteResult = await _mediaService.DeleteAsync(mediaId);
        Assert.True(deleteResult.IsSuccess);

        var inDb = await _dbContext.Media.FindAsync(mediaId);
        Assert.Null(inDb);
        Assert.False(_fileStorage.ContainsFile(storagePath));
    }

    [Fact]
    public async Task DownloadAsync_WhenExists_ReturnsStream()
    {
        var originalData = "test file data for download"u8.ToArray();
        using var stream = new MemoryStream(originalData);
        var uploadResult = await _mediaService.UploadAsync(null, stream, "doc.pdf", "application/pdf");
        Assert.True(uploadResult.IsSuccess);

        var downloadResult = await _mediaService.DownloadAsync(uploadResult.Value.Id);
        Assert.True(downloadResult.IsSuccess);

        using var ms = new MemoryStream();
        await downloadResult.Value.CopyToAsync(ms);
        Assert.Equal(originalData, ms.ToArray());
    }
}
