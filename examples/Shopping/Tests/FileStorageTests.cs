using System.Text;
using Kernel.Common;
using Kernel.Configuration;
using Kernel.Json;
using Kernel.Storage;
using Kernel.Storage.Extensions;
using Kernel.Testing.Assertions;
using Kernel.Testing.Fakes;
using Kernel.Testing.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Shopping.Tests;

public class FileStorageTests : IDisposable
{
    private readonly string _testStorageDir;
    private readonly JsonSerializer _jsonSerializer;

    public FileStorageTests()
    {
        _testStorageDir = Path.Combine(Path.GetTempPath(), $"KernelStorageTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testStorageDir);
        _jsonSerializer = new JsonSerializer();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testStorageDir))
            {
                Directory.Delete(_testStorageDir, recursive: true);
            }
        }
        catch { }
    }

    private FileStorageSettings CreateSettings(
        string? basePath = null,
        string? baseUrl = "/files",
        long maxSizeBytes = 10 * 1024 * 1024,
        List<string>? allowedExtensions = null)
    {
        var settings = new FileStorageSettings(_jsonSerializer, "Testing") { AutoSave = false };
        settings.SetValue(nameof(FileStorageSettings.BasePath), basePath ?? _testStorageDir);
        settings.SetValue(nameof(FileStorageSettings.BaseUrl), baseUrl ?? "");
        settings.SetValue(nameof(FileStorageSettings.MaxFileSizeBytes), maxSizeBytes);
        settings.SetValue(nameof(FileStorageSettings.AllowedExtensions), allowedExtensions ?? []);
        return settings;
    }

    [Fact]
    public async Task LocalFileStorage_UploadAndDownload_ReturnsMatchingContent()
    {
        var settings = CreateSettings();
        var storage = new LocalFileStorage(settings);

        var originalContent = "Hello, Modular Web API storage!"u8.ToArray();
        using var uploadStream = new MemoryStream(originalContent);

        var uploadResult = await storage.UploadAsync("greetings/hello.txt", uploadStream);
        Assert.True(uploadResult.IsSuccess);
        Assert.Equal("greetings/hello.txt", uploadResult.Value);

        var downloadResult = await storage.DownloadAsync("greetings/hello.txt");
        Assert.True(downloadResult.IsSuccess);

        await using var downloadedStream = downloadResult.Value;
        using var ms = new MemoryStream();
        await downloadedStream.CopyToAsync(ms);

        Assert.Equal(originalContent, ms.ToArray());
    }

    [Fact]
    public async Task LocalFileStorage_UploadBytesAndDownloadBytes_ExtensionMethodsWork()
    {
        var settings = CreateSettings();
        var storage = new LocalFileStorage(settings);

        var sampleBytes = new byte[] { 1, 2, 3, 4, 5, 42, 100, 255 };
        var uploadResult = await storage.UploadAsync("binary/data.bin", sampleBytes);
        Assert.True(uploadResult.IsSuccess);

        var downloadResult = await storage.DownloadBytesAsync("binary/data.bin");
        Assert.True(downloadResult.IsSuccess);
        Assert.Equal(sampleBytes, downloadResult.Value);
    }

    [Fact]
    public async Task LocalFileStorage_UploadTextAndDownloadText_ExtensionMethodsWork()
    {
        var settings = CreateSettings();
        var storage = new LocalFileStorage(settings);

        var sampleText = "This is a UTF-8 test text with special chars: 🚀 ñoño €";
        var uploadResult = await storage.UploadTextAsync("notes/readme.md", sampleText);
        Assert.True(uploadResult.IsSuccess);

        var downloadResult = await storage.DownloadTextAsync("notes/readme.md");
        Assert.True(downloadResult.IsSuccess);
        Assert.Equal(sampleText, downloadResult.Value);
    }

    [Fact]
    public async Task LocalFileStorage_ExistsAsync_ReturnsTrueForExistingAndFalseForMissing()
    {
        var settings = CreateSettings();
        var storage = new LocalFileStorage(settings);

        var existsBefore = await storage.ExistsAsync("items/item.txt");
        Assert.True(existsBefore.IsSuccess);
        Assert.False(existsBefore.Value);

        await storage.UploadTextAsync("items/item.txt", "Item content");

        var existsAfter = await storage.ExistsAsync("items/item.txt");
        Assert.True(existsAfter.IsSuccess);
        Assert.True(existsAfter.Value);

        await storage.DeleteAsync("items/item.txt");

        var existsDeleted = await storage.ExistsAsync("items/item.txt");
        Assert.True(existsDeleted.IsSuccess);
        Assert.False(existsDeleted.Value);
    }

    [Fact]
    public async Task LocalFileStorage_DeleteAsync_RemovesFileAndIsIdempotent()
    {
        var settings = CreateSettings();
        var storage = new LocalFileStorage(settings);

        await storage.UploadTextAsync("to_delete.txt", "Will be deleted");
        Assert.True((await storage.ExistsAsync("to_delete.txt")).Value);

        var deleteResult = await storage.DeleteAsync("to_delete.txt");
        Assert.True(deleteResult.IsSuccess);
        Assert.False((await storage.ExistsAsync("to_delete.txt")).Value);

        // Deleting already deleted or nonexistent file should succeed idempotently
        var deleteAgain = await storage.DeleteAsync("to_delete.txt");
        Assert.True(deleteAgain.IsSuccess);
    }

    [Fact]
    public async Task LocalFileStorage_GetUrlAsync_ReturnsCorrectFormattedUrl()
    {
        var settingsWithPrefix = CreateSettings(baseUrl: "https://cdn.example.com/assets");
        var storageWithPrefix = new LocalFileStorage(settingsWithPrefix);

        var urlResult1 = await storageWithPrefix.GetUrlAsync("images/logo.png");
        Assert.True(urlResult1.IsSuccess);
        Assert.Equal("https://cdn.example.com/assets/images/logo.png", urlResult1.Value);

        var settingsEmptyPrefix = CreateSettings(baseUrl: "");
        var storageEmptyPrefix = new LocalFileStorage(settingsEmptyPrefix);

        var urlResult2 = await storageEmptyPrefix.GetUrlAsync("images/logo.png");
        Assert.True(urlResult2.IsSuccess);
        Assert.Equal("/images/logo.png", urlResult2.Value);
    }

    [Fact]
    public async Task LocalFileStorage_PathTraversalAttack_IsBlocked()
    {
        var settings = CreateSettings();
        var storage = new LocalFileStorage(settings);

        var maliciousPaths = new[]
        {
            "../../etc/passwd",
            @"..\..\secret.txt",
            "folder/../../outside.txt",
            "subfolder/../../../Windows/System32/cmd.exe"
        };

        foreach (var path in maliciousPaths)
        {
            var uploadResult = await storage.UploadTextAsync(path, "Malicious payload");
            Assert.True(uploadResult.IsFailure);
            Assert.Contains("traversal", uploadResult.Error!.Message, StringComparison.OrdinalIgnoreCase);

            var downloadResult = await storage.DownloadAsync(path);
            Assert.True(downloadResult.IsFailure);
            Assert.Contains("traversal", downloadResult.Error!.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task LocalFileStorage_MaxFileSizeExceeded_FailsUpload()
    {
        // 50 bytes limit
        var settings = CreateSettings(maxSizeBytes: 50);
        var storage = new LocalFileStorage(settings);

        var largeData = new byte[100];
        Array.Fill(largeData, (byte)'A');

        var result = await storage.UploadAsync("large_file.bin", largeData);
        Assert.True(result.IsFailure);
        Assert.Contains("exceeds maximum allowed size", result.Error!.Message, StringComparison.OrdinalIgnoreCase);

        // Verify file was not written or was deleted
        var exists = await storage.ExistsAsync("large_file.bin");
        Assert.False(exists.Value);
    }

    [Fact]
    public async Task LocalFileStorage_AllowedExtensions_RestrictsUnallowedExtensions()
    {
        var settings = CreateSettings(allowedExtensions: [".jpg", ".png", ".pdf"]);
        var storage = new LocalFileStorage(settings);

        var validUpload = await storage.UploadTextAsync("document.pdf", "PDF header");
        Assert.True(validUpload.IsSuccess);

        var invalidUpload = await storage.UploadTextAsync("script.exe", "Executable bytes");
        Assert.True(invalidUpload.IsFailure);
        Assert.Contains("extension", invalidUpload.Error!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LocalFileStorage_AutoCreateDirectory_CreatesNestedFolders()
    {
        var settings = CreateSettings();
        var storage = new LocalFileStorage(settings);

        var nestedPath = "orders/2026/q3/invoices/INV-10023.txt";
        var result = await storage.UploadTextAsync(nestedPath, "Invoice total: $500");

        Assert.True(result.IsSuccess);
        Assert.True((await storage.ExistsAsync(nestedPath)).Value);

        var fullDiskPath = Path.Combine(_testStorageDir, "orders", "2026", "q3", "invoices", "INV-10023.txt");
        Assert.True(File.Exists(fullDiskPath));
    }

    [Fact]
    public async Task FakeFileStorage_UploadDownloadDelete_TracksStateCorrectly()
    {
        var fake = new FakeFileStorage();

        Assert.Equal(0, fake.Count);
        Assert.Equal(0, fake.UploadCount);
        Assert.Equal(0, fake.DownloadCount);
        Assert.Equal(0, fake.DeleteCount);

        var uploadResult = await fake.UploadTextAsync("users/avatar.png", "PNG Data");
        Assert.True(uploadResult.IsSuccess);
        Assert.Equal(1, fake.Count);
        Assert.Equal(1, fake.UploadCount);
        Assert.True(fake.ContainsFile("users/avatar.png"));

        var downloadResult = await fake.DownloadTextAsync("users/avatar.png");
        Assert.True(downloadResult.IsSuccess);
        Assert.Equal("PNG Data", downloadResult.Value);
        Assert.Equal(1, fake.DownloadCount);

        var deleteResult = await fake.DeleteAsync("users/avatar.png");
        Assert.True(deleteResult.IsSuccess);
        Assert.Equal(0, fake.Count);
        Assert.Equal(1, fake.DeleteCount);
        Assert.Contains("users/avatar.png", fake.DeletedPaths);
    }

    [Fact]
    public async Task FakeFileStorage_SimulateFailures_ReturnsErrorResults()
    {
        var fake = new FakeFileStorage
        {
            SimulateFailureOnUpload = true,
            SimulateFailureOnDownload = true,
            SimulateFailureOnDelete = true
        };

        var uploadResult = await fake.UploadTextAsync("test.txt", "content");
        Assert.True(uploadResult.IsFailure);

        var downloadResult = await fake.DownloadAsync("test.txt");
        Assert.True(downloadResult.IsFailure);

        var deleteResult = await fake.DeleteAsync("test.txt");
        Assert.True(deleteResult.IsFailure);
    }

    [Fact]
    public async Task FileStorageAssertions_FluentMethods_AssertCorrectly()
    {
        var fake = new FakeFileStorage();

        fake.ShouldBeEmpty();
        fake.ShouldHaveCount(0);
        fake.ShouldNotHaveFile("report.pdf");

        await fake.UploadTextAsync("report.pdf", "Financial Report 2026");

        fake.ShouldHaveFile("report.pdf");
        fake.ShouldHaveFile("report.pdf", "Financial Report 2026");
        fake.ShouldHaveCount(1);

        await fake.DeleteAsync("report.pdf");

        fake.ShouldHaveDeleted("report.pdf");
        fake.ShouldNotHaveFile("report.pdf");
        fake.ShouldBeEmpty();
    }

    [Fact]
    public async Task DynamicSettings_StorageSettingsMutation_UpdatesConfigurationLive()
    {
        var settings = CreateSettings();
        var dynamicSettings = new DynamicSettings(_jsonSerializer, "Testing");
        dynamicSettings.AddSection(settings);

        var retrievedSettings = dynamicSettings.GetSection<FileStorageSettings>();
        Assert.NotNull(retrievedSettings);
        Assert.Equal(_testStorageDir, retrievedSettings.BasePath);

        var eventRaised = false;
        dynamicSettings.OnSettingChanged += (sec, key) =>
        {
            if (sec.SectionName == "Storage" && key == "BasePath")
            {
                eventRaised = true;
            }
        };

        var newPath = Path.Combine(Path.GetTempPath(), "NewStorageDir");
        retrievedSettings.SetValue(nameof(FileStorageSettings.BasePath), newPath);

        Assert.Equal(newPath, retrievedSettings.BasePath);
        Assert.True(eventRaised);
    }

    private class TestKernelBaseFixture : KernelTestBase
    {
    }

    [Fact]
    public async Task KernelTestBase_FileStorageIntegration_CanResolveAndUseStorage()
    {
        var fixture = new TestKernelBaseFixture();
        await fixture.InitializeAsync();

        var storage = fixture.Services.GetRequiredService<IFileStorage>();
        Assert.NotNull(storage);
        Assert.Same(fixture.FileStorage, storage);

        await storage.UploadTextAsync("test.json", "{\"key\":\"value\"}");
        fixture.FileStorage.ShouldHaveFile("test.json", "{\"key\":\"value\"}");

        await fixture.DisposeAsync();
    }
}
