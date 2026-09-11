using System.Net;
using System.Text;
using Kernel.Storage;
using Kernel.Storage.Extensions;
using Kernel.Testing.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Identity.Tests;

public class FileStorageRegistryTests : IClassFixture<TestWebApplicationFactory<Program>>
{
    private readonly TestWebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public FileStorageRegistryTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task GetUrlAsync_GeneratesOpaqueUlidUrl_AndHidesFileStructure()
    {
        var internalPath = "private/users/admin/sensitive_documents/financial_report.txt";
        var content = "Confidential Financial Data"u8.ToArray();

        var uploadResult = await _factory.FileStorage.UploadAsync(internalPath, content, "text/plain");
        Assert.True(uploadResult.IsSuccess);

        var urlResult = await _factory.FileStorage.GetUrlAsync(internalPath);
        Assert.True(urlResult.IsSuccess);

        var url = urlResult.Value;
        Assert.NotNull(url);
        Assert.StartsWith("/files/", url);

        // Verify the internal folder structure and filename are completely hidden
        Assert.DoesNotContain("private", url);
        Assert.DoesNotContain("admin", url);
        Assert.DoesNotContain("sensitive_documents", url);
        Assert.DoesNotContain("financial_report.txt", url);

        // The ID segment must parse into a valid DefaultId (Ulid)
        var idPart = url["/files/".Length..];
        Assert.True(DefaultId.TryParse(idPart, out var ulid));
        Assert.NotEqual(default, ulid);
    }

    [Fact]
    public async Task GetPublicFile_WithValidId_ReturnsContentAndCorrectContentType()
    {
        var path = "assets/images/logo.png";
        var pngHeader = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        await _factory.FileStorage.UploadAsync(path, pngHeader, "image/png");
        var urlResult = await _factory.FileStorage.GetUrlAsync(path);
        Assert.True(urlResult.IsSuccess);

        var response = await _client.GetAsync(urlResult.Value);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);

        var downloadedBytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(pngHeader, downloadedBytes);
    }

    [Fact]
    public async Task GetPublicFile_WithDownloadParam_ReturnsAttachmentContentDisposition()
    {
        var path = "exports/data_export.csv";
        var text = "id,name,value\n1,Alpha,100";

        await _factory.FileStorage.UploadTextAsync(path, text);
        var urlResult = await _factory.FileStorage.GetUrlAsync(path);
        Assert.True(urlResult.IsSuccess);

        var response = await _client.GetAsync(urlResult.Value + "?download=true");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.NotNull(response.Content.Headers.ContentDisposition);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition.DispositionType);
        Assert.Equal("data_export.csv", response.Content.Headers.ContentDisposition.FileName);

        var downloadedText = await response.Content.ReadAsStringAsync();
        Assert.Equal(text, downloadedText);
    }

    [Fact]
    public async Task GetPublicFile_WithNonExistentId_ReturnsNotFound()
    {
        var nonExistentId = DefaultId.NewUlid();
        var response = await _client.GetAsync($"/files/{nonExistentId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetPublicFile_WithInvalidId_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/files/invalid-not-a-ulid");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetPublicFile_WithExpiredUrl_ReturnsNotFound()
    {
        var path = "temp/temporary_token.txt";
        await _factory.FileStorage.UploadTextAsync(path, "temporary secret");

        var urlResult = await _factory.FileStorage.GetUrlAsync(path, expiry: TimeSpan.FromMilliseconds(50));
        Assert.True(urlResult.IsSuccess);

        // Wait for expiry
        await Task.Delay(100);

        var response = await _client.GetAsync(urlResult.Value);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteAsync_RevokesPublicAccessImmediately()
    {
        var path = "documents/revocation_test.txt";
        await _factory.FileStorage.UploadTextAsync(path, "will be deleted");

        var urlResult = await _factory.FileStorage.GetUrlAsync(path);
        Assert.True(urlResult.IsSuccess);

        // Ensure file is publicly accessible first
        var responseBefore = await _client.GetAsync(urlResult.Value);
        Assert.Equal(HttpStatusCode.OK, responseBefore.StatusCode);

        // Delete file
        var deleteResult = await _factory.FileStorage.DeleteAsync(path);
        Assert.True(deleteResult.IsSuccess);

        // Public URL must now return 404 Not Found
        var responseAfter = await _client.GetAsync(urlResult.Value);
        Assert.Equal(HttpStatusCode.NotFound, responseAfter.StatusCode);
    }

    [Fact]
    public async Task PublicAccessDisabled_BlocksGetUrlAndEndpointReturnsNotFound()
    {
        var path = "secure/secret.txt";
        await _factory.FileStorage.UploadTextAsync(path, "secret content");

        var dynamicSettings = _factory.Services.GetRequiredService<Kernel.Configuration.DynamicSettings>();
        var storageSettings = dynamicSettings.GetSection<FileStorageSettings>();

        var originalFakeSetting = _factory.FileStorage.EnablePublicAccess;
        var originalSetting = storageSettings.EnablePublicAccess;
        try
        {
            _factory.FileStorage.EnablePublicAccess = false;
            storageSettings.AutoSave = false;
            storageSettings.SetValue(nameof(FileStorageSettings.EnablePublicAccess), false);

            // GetUrlAsync should fail when disabled in settings
            var urlResult = await _factory.FileStorage.GetUrlAsync(path);
            Assert.True(urlResult.IsFailure);
            Assert.Contains("disabled", urlResult.Error?.Message, StringComparison.OrdinalIgnoreCase);

            // Directly querying any ID should return 404 when disabled
            var testId = DefaultId.NewUlid();
            var response = await _client.GetAsync($"/files/{testId}");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        finally
        {
            _factory.FileStorage.EnablePublicAccess = originalFakeSetting;
            storageSettings.SetValue(nameof(FileStorageSettings.EnablePublicAccess), originalSetting);
        }
    }

    [Fact]
    public async Task DbFileRegistry_WithKernelDbContext_RegistersRetrievesAndRevokes()
    {
        var scopeFactory = _factory.Services.GetRequiredService<IServiceScopeFactory>();
        var registry = new DbFileRegistry(scopeFactory);

        var path = "uploads/document.pdf";
        var regResult = await registry.RegisterAsync(path);
        Assert.True(regResult.IsSuccess);

        var getResult = await registry.GetAsync(regResult.Value);
        Assert.True(getResult.IsSuccess);
        Assert.NotNull(getResult.Value);
        Assert.Equal(path, getResult.Value.Path);
        Assert.False(getResult.Value.IsExpired);

        // Revoke by ID
        var revokeResult = await registry.RevokeAsync(regResult.Value);
        Assert.True(revokeResult.IsSuccess);

        var afterRevoke = await registry.GetAsync(regResult.Value);
        Assert.True(afterRevoke.IsFailure);
    }

    [Fact]
    public async Task LocalFileStorage_WithDbFileRegistry_UploadsAndGeneratesUrl()
    {
        var scopeFactory = _factory.Services.GetRequiredService<IServiceScopeFactory>();
        var registry = new DbFileRegistry(scopeFactory);
        var settings = _factory.Services.GetRequiredService<FileStorageSettings>();

        var local = new LocalFileStorage(settings, registry);
        var path = "direct_test/sample.txt";
        using var stream = new MemoryStream("Hello from local file storage"u8.ToArray());

        var uploadResult = await local.UploadAsync(path, stream, "text/plain");
        Assert.True(uploadResult.IsSuccess);

        var urlResult = await local.GetUrlAsync(path);
        Assert.True(urlResult.IsSuccess);
        Assert.StartsWith("/files/", urlResult.Value);

        // Clean up
        await local.DeleteAsync(path);
    }
}
