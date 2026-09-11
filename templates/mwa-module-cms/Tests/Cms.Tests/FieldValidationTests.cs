using Cms.Configuration;
using Cms.ContentTypes;
using Cms.ContentTypes.BuiltIn;
using Cms.Persistence;
using Cms.Services;
using Kernel.Context;
using Kernel.Events;
using Kernel.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cms.Tests;

public class FieldValidationTests
{
    private readonly ContentService _contentService;

    public FieldValidationTests()
    {
        var options = new DbContextOptionsBuilder<CmsModuleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var dbContext = new CmsModuleDbContext(options);
        var registry = new ContentTypeRegistry();
        registry.Register<PageContentType>();
        registry.Register<PostContentType>();
        registry.Register<FaqContentType>();

        var settings = new CmsSettings(new JsonSerializer());
        var slugService = new SlugService(dbContext, settings);
        var eventBus = new Kernel.Testing.Fakes.FakeEventBus();
        var scopeContext = new TestScopeContext();
        var logger = NullLogger<ContentService>.Instance;

        _contentService = new ContentService(
            dbContext,
            registry,
            slugService,
            eventBus,
            scopeContext,
            settings,
            logger);
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

    [Fact]
    public void ValidateFields_Page_AllowsEmptyFields()
    {
        var result1 = _contentService.ValidateFields("Page", null);
        var result2 = _contentService.ValidateFields("Page", "{}");

        Assert.True(result1.IsSuccess);
        Assert.True(result2.IsSuccess);
    }

    [Fact]
    public void ValidateFields_Faq_MissingRequiredFields_Fails()
    {
        // Faq requires Question and Answer
        var emptyResult = _contentService.ValidateFields("Faq", null);
        Assert.True(emptyResult.IsFailure);

        var missingAnswerResult = _contentService.ValidateFields("Faq", """{"Question": "What is MWA?"}""");
        Assert.True(missingAnswerResult.IsFailure);
        Assert.Contains("Answer", missingAnswerResult.Messages.First());

        var missingQuestionResult = _contentService.ValidateFields("Faq", """{"Answer": "A modular architecture."}""");
        Assert.True(missingQuestionResult.IsFailure);
        Assert.Contains("Question", missingQuestionResult.Messages.First());
    }

    [Fact]
    public void ValidateFields_Faq_WithRequiredFields_Succeeds()
    {
        var json = """
        {
            "Question": "What is Modular Web API?",
            "Answer": "It is an architecture for ASP.NET Core modular monoliths."
        }
        """;

        var result = _contentService.ValidateFields("Faq", json);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ValidateFields_Faq_MaxLengthExceeded_Fails()
    {
        var longQuestion = new string('x', 501);
        var json = $$"""
        {
            "Question": "{{longQuestion}}",
            "Answer": "Valid answer."
        }
        """;

        var result = _contentService.ValidateFields("Faq", json);
        Assert.True(result.IsFailure);
        Assert.Contains("exceeds maximum length", result.Messages.First());
    }

    [Fact]
    public void ValidateFields_Post_ValidFields_Succeeds()
    {
        var json = """
        {
            "FeaturedImageUrl": "https://example.com/image.jpg",
            "IsFeatured": true
        }
        """;

        var result = _contentService.ValidateFields("Post", json);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ValidateFields_Post_InvalidType_Fails()
    {
        // IsFeatured must be a boolean, passing a string should fail
        var json = """
        {
            "IsFeatured": "yes"
        }
        """;

        var result = _contentService.ValidateFields("Post", json);
        Assert.True(result.IsFailure);
        Assert.Contains("must be a boolean", result.Messages.First());
    }

    [Fact]
    public void ValidateFields_InvalidJson_Fails()
    {
        var result = _contentService.ValidateFields("Post", "invalid-json-string");
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void ValidateFields_UnregisteredContentType_Fails()
    {
        var result = _contentService.ValidateFields("NonExistentType", "{}");
        Assert.True(result.IsFailure);
        Assert.Contains("not registered", result.Messages.First());
    }
}
