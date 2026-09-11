using Cms.Controllers;
using Cms.Contracts.Constants;
using Cms.Contracts.Dtos;
using Cms.Features.Content;
using Cms.Features.ContentTypes;
using Cms.Features.Media;
using Cms.Features.Public;
using Cms.Features.Revisions;
using Cms.Features.Tags;
using Kernel.Common;
using Kernel.Context;
using Kernel.Requests;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;
using static Cms.Controllers.ContentController;
using static Cms.Controllers.TagsController;

namespace Cms.Tests;

public class ControllersTests
{
    private class FakeDispatcher : IDispatcher
    {
        public Task<Result<TResponse>> DispatchAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request is CreateContent cc)
            {
                var dto = new ContentItemDetailDto(
                    DefaultId.NewUlid(), cc.ContentType, cc.Title, "slug", cc.Body, cc.Excerpt,
                    cc.FieldsJson, null, ContentStatus.Draft, cc.AuthorId, null, null, 1, 0, [], [], DateTime.UtcNow);
                return Task.FromResult(Result.Success((TResponse)(object)dto));
            }

            if (request is GetContent gc)
            {
                if (gc.Id == DefaultId.Empty)
                {
                    return Task.FromResult(Result.Failure<TResponse>(new KeyNotFoundException("Content not found.")));
                }

                var dto = new ContentItemDetailDto(
                    gc.Id, "Page", "Title", "slug", "Body", null, null, null, ContentStatus.Draft,
                    DefaultId.NewUlid(), null, null, 1, 0, [], [], DateTime.UtcNow);
                return Task.FromResult(Result.Success((TResponse)(object)dto));
            }

            if (request is GetContents)
            {
                var paged = PaginatedResult<ContentItemDto>.Empty(1, 20);
                return Task.FromResult(Result.Success((TResponse)(object)paged));
            }

            if (request is UpdateContent uc)
            {
                var dto = new ContentItemDetailDto(
                    uc.Id, "Page", uc.Title ?? "Updated", "updated", uc.Body, uc.Excerpt,
                    uc.FieldsJson, uc.MetadataJson, ContentStatus.Draft, DefaultId.NewUlid(),
                    null, null, 2, 0, [], [], DateTime.UtcNow);
                return Task.FromResult(Result.Success((TResponse)(object)dto));
            }

            if (request is DeleteContent or PublishContent or UnpublishContent or ArchiveContent or SetContentTags)
            {
                return Task.FromResult(Result.Success((TResponse)(object)true));
            }

            if (request is GetRevisions gr)
            {
                var list = (IReadOnlyList<ContentRevisionDto>)new List<ContentRevisionDto>
                {
                    new(DefaultId.NewUlid(), 1, DefaultId.NewUlid(), "Initial", DateTime.UtcNow)
                };
                return Task.FromResult(Result.Success((TResponse)(object)list));
            }

            if (request is GetRevision)
            {
                var dto = new ContentRevisionDetailDto(
                    DefaultId.NewUlid(), 1, "Title", "Body", null, null, null, DefaultId.NewUlid(), null, DateTime.UtcNow);
                return Task.FromResult(Result.Success((TResponse)(object)dto));
            }

            if (request is RestoreRevision rr)
            {
                var dto = new ContentItemDetailDto(
                    rr.ContentId, "Page", "Restored", "restored", "Body", null, null, null,
                    ContentStatus.Draft, DefaultId.NewUlid(), null, null, 3, 0, [], [], DateTime.UtcNow);
                return Task.FromResult(Result.Success((TResponse)(object)dto));
            }

            if (request is CompareRevisions cr)
            {
                var dto = new RevisionComparisonDto(cr.FromRevision, cr.ToRevision, []);
                return Task.FromResult(Result.Success((TResponse)(object)dto));
            }

            if (request is GetContentTypes)
            {
                var list = (IReadOnlyList<ContentTypeDto>)new List<ContentTypeDto>
                {
                    new("Page", "Generic page", "page", true)
                };
                return Task.FromResult(Result.Success((TResponse)(object)list));
            }

            if (request is GetContentType gct)
            {
                if (gct.Name == "NotFound")
                {
                    return Task.FromResult(Result.Failure<TResponse>(new KeyNotFoundException("Type not found.")));
                }

                var dto = new ContentTypeDetailDto(
                    gct.Name, "Desc", "icon", true, "markdown", true, true, null, []);
                return Task.FromResult(Result.Success((TResponse)(object)dto));
            }

            if (request is CreateTag ct)
            {
                var dto = new ContentTagDto(DefaultId.NewUlid(), ct.Name, "tag-slug", ct.Description);
                return Task.FromResult(Result.Success((TResponse)(object)dto));
            }

            if (request is GetTags)
            {
                var paged = PaginatedResult<ContentTagDto>.Empty(1, 20);
                return Task.FromResult(Result.Success((TResponse)(object)paged));
            }

            if (request is GetTag gt)
            {
                var dto = new ContentTagDto(gt.Id, "Sample Tag", "sample-tag", null);
                return Task.FromResult(Result.Success((TResponse)(object)dto));
            }

            if (request is UpdateTag ut)
            {
                var dto = new ContentTagDto(ut.Id, ut.Name ?? "Updated Tag", "sample-tag", ut.Description);
                return Task.FromResult(Result.Success((TResponse)(object)dto));
            }

            if (request is DeleteTag)
            {
                return Task.FromResult(Result.Success((TResponse)(object)true));
            }

            if (request is UploadMedia um)
            {
                var dto = new ContentMediaDto(
                    DefaultId.NewUlid(), um.FileName, um.MimeType, 100, um.AltText, null, null, "url", DateTime.UtcNow);
                return Task.FromResult(Result.Success((TResponse)(object)dto));
            }

            if (request is GetMedia)
            {
                var paged = PaginatedResult<ContentMediaDto>.Empty(1, 20);
                return Task.FromResult(Result.Success((TResponse)(object)paged));
            }

            if (request is GetMediaItem gmi)
            {
                var dto = new ContentMediaDto(
                    gmi.Id, "file.jpg", "image/jpeg", 50, null, null, null, "url", DateTime.UtcNow);
                return Task.FromResult(Result.Success((TResponse)(object)dto));
            }

            if (request is DeleteMedia)
            {
                return Task.FromResult(Result.Success((TResponse)(object)true));
            }

            if (request is GetContentMedia)
            {
                var list = (IReadOnlyList<ContentMediaDto>)new List<ContentMediaDto>();
                return Task.FromResult(Result.Success((TResponse)(object)list));
            }

            if (request is GetPublishedContents)
            {
                var paged = PaginatedResult<ContentItemDto>.Empty(1, 20);
                return Task.FromResult(Result.Success((TResponse)(object)paged));
            }

            if (request is GetPublishedContentBySlug gpcs)
            {
                if (gpcs.Slug == "not-found")
                {
                    return Task.FromResult(Result.Failure<TResponse>(new KeyNotFoundException("Not found.")));
                }

                var dto = new ContentItemDetailDto(
                    DefaultId.NewUlid(), gpcs.ContentType, "Public Title", gpcs.Slug, "Body",
                    null, null, null, ContentStatus.Published, DefaultId.NewUlid(), null, DateTime.UtcNow,
                    1, 0, [], [], DateTime.UtcNow);
                return Task.FromResult(Result.Success((TResponse)(object)dto));
            }

            if (request is GetPublicTags)
            {
                var list = (IReadOnlyList<PublicTagDto>)new List<PublicTagDto>
                {
                    new(DefaultId.NewUlid(), "News", "news", null, 5)
                };
                return Task.FromResult(Result.Success((TResponse)(object)list));
            }

            if (request is GetPublicTagBySlug gpts)
            {
                var dto = new PublicTagDetailDto(
                    DefaultId.NewUlid(), "News", gpts.Slug, null, []);
                return Task.FromResult(Result.Success((TResponse)(object)dto));
            }

            throw new NotImplementedException($"No fake setup for {request.GetType().Name}");
        }
    }

    private class TestScopeContext : IScopeContext
    {
        public DefaultId? UserId => DefaultId.NewUlid();
        public bool IsAuthenticated => true;
        public IReadOnlyList<string> Roles => ["Admin"];
        public IReadOnlyDictionary<string, string> Claims => new Dictionary<string, string>();
        public string? GetClaim(string type) => null;
        public IReadOnlyList<string> GetClaimValues(string type) => [];
        public bool IsInRole(string role) => true;
        public bool IsSystem => false;
        public string? CorrelationId => null;
    }

    private readonly FakeDispatcher _dispatcher = new();
    private readonly TestScopeContext _scopeContext = new();

    [Fact]
    public async Task ContentController_Create_ReturnsCreatedAtAction()
    {
        var controller = new ContentController(_dispatcher, _scopeContext);
        var request = new CreateContentRequest("Page", "Test Page", "Body");

        var result = await controller.Create(request, CancellationToken.None);

        var actionResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var dto = Assert.IsType<ContentItemDetailDto>(actionResult.Value);
        Assert.Equal("Test Page", dto.Title);
    }

    [Fact]
    public async Task ContentController_GetById_WhenExists_ReturnsOk()
    {
        var controller = new ContentController(_dispatcher, _scopeContext);
        var id = DefaultId.NewUlid();

        var result = await controller.GetById(id, CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<ContentItemDetailDto>(okResult.Value);
        Assert.Equal(id, dto.Id);
    }

    [Fact]
    public async Task ContentController_StatusTransitions_ReturnOk()
    {
        var controller = new ContentController(_dispatcher, _scopeContext);
        var id = DefaultId.NewUlid();

        var pubResult = await controller.Publish(id, CancellationToken.None);
        Assert.IsType<OkObjectResult>(pubResult);

        var unpubResult = await controller.Unpublish(id, CancellationToken.None);
        Assert.IsType<OkObjectResult>(unpubResult);

        var archResult = await controller.Archive(id, CancellationToken.None);
        Assert.IsType<OkObjectResult>(archResult);
    }

    [Fact]
    public async Task ContentPublicController_Endpoints_WorkCorrectly()
    {
        var controller = new ContentPublicController(_dispatcher);

        var listResult = await controller.GetPublishedContent();
        var okList = Assert.IsType<OkObjectResult>(listResult.Result);
        Assert.NotNull(okList.Value);

        var slugResult = await controller.GetPublishedContentBySlug("Page", "home", CancellationToken.None);
        var okSlug = Assert.IsType<OkObjectResult>(slugResult.Result);
        Assert.NotNull(okSlug.Value);

        var tagsResult = await controller.GetTags(CancellationToken.None);
        var okTags = Assert.IsType<OkObjectResult>(tagsResult.Result);
        Assert.NotNull(okTags.Value);

        var tagSlugResult = await controller.GetTagBySlug("news", CancellationToken.None);
        var okTagSlug = Assert.IsType<OkObjectResult>(tagSlugResult.Result);
        Assert.NotNull(okTagSlug.Value);
    }

    [Fact]
    public async Task ContentTypesController_GetAllAndGetByName_Work()
    {
        var controller = new ContentTypesController(_dispatcher);

        var allResult = await controller.GetAll(CancellationToken.None);
        var okAll = Assert.IsType<OkObjectResult>(allResult.Result);
        Assert.NotNull(okAll.Value);

        var nameResult = await controller.GetByName("Page", CancellationToken.None);
        var okName = Assert.IsType<OkObjectResult>(nameResult.Result);
        Assert.NotNull(okName.Value);
    }

    [Fact]
    public async Task TagsController_Crud_Works()
    {
        var controller = new TagsController(_dispatcher);

        var createResult = await controller.Create(new CreateTagRequest("Tag 1"), CancellationToken.None);
        var created = Assert.IsType<CreatedAtActionResult>(createResult.Result);
        Assert.NotNull(created.Value);

        var id = DefaultId.NewUlid();
        var getResult = await controller.GetById(id, CancellationToken.None);
        var okGet = Assert.IsType<OkObjectResult>(getResult.Result);
        Assert.NotNull(okGet.Value);

        var updateResult = await controller.Update(id, new UpdateTagRequest("Updated"), CancellationToken.None);
        var okUpdate = Assert.IsType<OkObjectResult>(updateResult.Result);
        Assert.NotNull(okUpdate.Value);

        var deleteResult = await controller.Delete(id, CancellationToken.None);
        Assert.IsType<NoContentResult>(deleteResult);
    }

    [Fact]
    public async Task MediaController_Endpoints_Work()
    {
        var controller = new MediaController(_dispatcher);

        var formFile = new FormFile(new MemoryStream("file"u8.ToArray()), 0, 4, "file", "avatar.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };

        var uploadResult = await controller.Upload(formFile, ct: CancellationToken.None);
        var created = Assert.IsType<CreatedAtActionResult>(uploadResult.Result);
        Assert.NotNull(created.Value);

        var id = DefaultId.NewUlid();
        var getResult = await controller.GetById(id, CancellationToken.None);
        var okGet = Assert.IsType<OkObjectResult>(getResult.Result);
        Assert.NotNull(okGet.Value);

        var deleteResult = await controller.Delete(id, CancellationToken.None);
        Assert.IsType<NoContentResult>(deleteResult);
    }
}
