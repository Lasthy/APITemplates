using System.Text;
using Kernel.Common;
using Kernel.Storage;
using Xunit;

namespace Kernel.Tests.Storage;

public class LocalFileStorageTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly FakeFileRegistry _fakeRegistry = new();

    private class FakeFileRegistry : IFileRegistry
    {
        public Task<Result<DefaultId>> RegisterAsync(string path, TimeSpan? expiry = null, CancellationToken ct = default) =>
            Task.FromResult(Result.Success(DefaultId.NewUlid()));

        public Task<Result<FileRegistryEntry>> GetAsync(DefaultId id, CancellationToken ct = default) =>
            Task.FromResult(Result.Failure<FileRegistryEntry>(new KeyNotFoundException()));

        public Task<Result> RevokeAsync(DefaultId id, CancellationToken ct = default) =>
            Task.FromResult(Result.Success());

        public Task<Result> RevokeByPathAsync(string path, CancellationToken ct = default) =>
            Task.FromResult(Result.Success());
    }

    public LocalFileStorageTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"StorageTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            try { Directory.Delete(_tempDirectory, recursive: true); } catch { }
        }
    }

    private LocalFileStorage CreateStorage(List<string>? allowedExtensions = null)
    {
        var serializer = new Kernel.Json.JsonSerializer();
        var settings = new FileStorageSettings(serializer)
        {
            AutoSave = false
        };
        settings.SetValue("BasePath", _tempDirectory);
        settings.SetValue("MaxFileSizeBytes", 1024L * 1024L);
        if (allowedExtensions != null)
        {
            settings.SetValue("AllowedExtensions", allowedExtensions);
        }

        return new LocalFileStorage(settings, _fakeRegistry);
    }

    [Fact]
    public async Task UploadAsync_And_DownloadAsync_Roundtrip()
    {
        var storage = CreateStorage();
        var content = "Hello, Local File Storage!";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        var uploadResult = await storage.UploadAsync("documents/test.txt", stream);
        Assert.True(uploadResult.IsSuccess);

        var existsResult = await storage.ExistsAsync("documents/test.txt");
        Assert.True(existsResult.IsSuccess && existsResult.Value);

        var downloadResult = await storage.DownloadAsync("documents/test.txt");
        Assert.True(downloadResult.IsSuccess);

        using var reader = new StreamReader(downloadResult.Value);
        var readContent = await reader.ReadToEndAsync();
        Assert.Equal(content, readContent);
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveFile()
    {
        var storage = CreateStorage();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Data"));

        await storage.UploadAsync("temp.txt", stream);
        Assert.True((await storage.ExistsAsync("temp.txt")).Value);

        var deleteResult = await storage.DeleteAsync("temp.txt");
        Assert.True(deleteResult.IsSuccess);

        Assert.False((await storage.ExistsAsync("temp.txt")).Value);
    }

    [Fact]
    public async Task UploadAsync_PathTraversal_ShouldBeBlocked()
    {
        var storage = CreateStorage();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Malicious Content"));

        var result = await storage.UploadAsync("../../../Windows/System32/evil.txt", stream);

        Assert.True(result.IsFailure);
        Assert.Contains("traversal", result.Error?.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UploadAsync_DisallowedExtension_ShouldFail()
    {
        var storage = CreateStorage(allowedExtensions: new List<string> { ".jpg", ".png" });
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Executable Code"));

        var result = await storage.UploadAsync("malware.exe", stream);

        Assert.True(result.IsFailure);
        Assert.Contains("not allowed", result.Error?.Message, StringComparison.OrdinalIgnoreCase);
    }
}
