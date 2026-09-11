using Cms.Entities;
using Cms.Features.Revisions;
using Cms.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Cms.Tests;

public class RevisionsTests : IDisposable
{
    private readonly CmsModuleDbContext _dbContext;

    public RevisionsTests()
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
    public async Task CompareRevisions_DetectsDifferencesCorrectly()
    {
        var contentId = DefaultId.NewUlid();
        var authorId = DefaultId.NewUlid();

        var rev1 = new ContentRevision
        {
            Id = DefaultId.NewUlid(),
            ContentItemId = contentId,
            RevisionNumber = 1,
            Title = "First Title",
            Body = "First body content",
            Excerpt = "Initial excerpt",
            FieldsJson = """{"Question": "Q1?", "Answer": "A1"}""",
            MetadataJson = null,
            AuthorId = authorId,
            CreatedAt = DateTime.UtcNow.AddMinutes(-10)
        };

        var rev2 = new ContentRevision
        {
            Id = DefaultId.NewUlid(),
            ContentItemId = contentId,
            RevisionNumber = 2,
            Title = "Second Title",
            Body = "First body content", // unchanged
            Excerpt = "Updated excerpt",
            FieldsJson = """{"Question": "Q1?", "Answer": "Updated A1"}""",
            MetadataJson = """{"seoTitle": "SEO"}""",
            AuthorId = authorId,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Revisions.AddRange(rev1, rev2);
        await _dbContext.SaveChangesAsync();

        var handler = new CompareRevisionsHandler(_dbContext);
        var result = await handler.HandleAsync(new CompareRevisions(contentId, 1, 2));

        Assert.True(result.IsSuccess);
        var comparison = result.Value;
        Assert.Equal(1, comparison.FromRevision);
        Assert.Equal(2, comparison.ToRevision);

        // Verify detected changes
        Assert.Contains(comparison.Changes, c => c.FieldName == "Title" && c.OldValue == "First Title" && c.NewValue == "Second Title");
        Assert.Contains(comparison.Changes, c => c.FieldName == "Excerpt" && c.OldValue == "Initial excerpt" && c.NewValue == "Updated excerpt");
        Assert.Contains(comparison.Changes, c => c.FieldName == "Fields.Answer" && c.OldValue == "A1" && c.NewValue == "Updated A1");
        Assert.DoesNotContain(comparison.Changes, c => c.FieldName == "Body"); // Body was unchanged
    }

    [Fact]
    public async Task GetRevisions_ReturnsAllRevisionsForContentItem()
    {
        var contentId = DefaultId.NewUlid();

        _dbContext.Revisions.AddRange(
            new ContentRevision { Id = DefaultId.NewUlid(), ContentItemId = contentId, RevisionNumber = 1, Title = "v1", AuthorId = DefaultId.NewUlid(), CreatedAt = DateTime.UtcNow.AddMinutes(-5) },
            new ContentRevision { Id = DefaultId.NewUlid(), ContentItemId = contentId, RevisionNumber = 2, Title = "v2", AuthorId = DefaultId.NewUlid(), CreatedAt = DateTime.UtcNow }
        );
        await _dbContext.SaveChangesAsync();

        var handler = new GetRevisionsHandler(_dbContext);
        var result = await handler.HandleAsync(new GetRevisions(contentId));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.Equal(2, result.Value.First().RevisionNumber); // descending
    }
}
