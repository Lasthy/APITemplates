using System.Text;
using Kernel.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kernel.Storage.Extensions;

/// <summary>
/// Extension methods for <see cref="IFileStorage"/> and storage service registration.
/// </summary>
public static class StorageExtensions
{
    /// <summary>
    /// Uploads a byte array to the storage provider at the specified path.
    /// </summary>
    public static async Task<Result<string>> UploadAsync(
        this IFileStorage storage,
        string path,
        byte[] data,
        string? contentType = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(data);

        using var memoryStream = new MemoryStream(data, writable: false);
        return await storage.UploadAsync(path, memoryStream, contentType, ct);
    }

    /// <summary>
    /// Downloads the entire file content from storage as a byte array.
    /// </summary>
    public static async Task<Result<byte[]>> DownloadBytesAsync(
        this IFileStorage storage,
        string path,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(storage);

        var downloadResult = await storage.DownloadAsync(path, ct);
        if (downloadResult.IsFailure)
        {
            return Result.Failure<byte[]>(downloadResult.Error!);
        }

        if (downloadResult.Value is not { } stream)
        {
            return Result.Failure<byte[]>(new InvalidOperationException("Download stream was empty."));
        }

        await using (stream)
        {
            if (stream is MemoryStream ms)
            {
                return Result.Success(ms.ToArray());
            }

            using var copyStream = new MemoryStream();
            await stream.CopyToAsync(copyStream, ct);
            return Result.Success(copyStream.ToArray());
        }
    }

    /// <summary>
    /// Uploads a text string to storage with the specified encoding.
    /// </summary>
    public static async Task<Result<string>> UploadTextAsync(
        this IFileStorage storage,
        string path,
        string text,
        Encoding? encoding = null,
        string? contentType = "text/plain",
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(text);

        var bytes = (encoding ?? Encoding.UTF8).GetBytes(text);
        return await storage.UploadAsync(path, bytes, contentType, ct);
    }

    /// <summary>
    /// Downloads a text file from storage and decodes it with the specified encoding.
    /// </summary>
    public static async Task<Result<string>> DownloadTextAsync(
        this IFileStorage storage,
        string path,
        Encoding? encoding = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(storage);

        var bytesResult = await storage.DownloadBytesAsync(path, ct);
        if (bytesResult.IsFailure)
        {
            return Result.Failure<string>(bytesResult.Error!);
        }

        var text = (encoding ?? Encoding.UTF8).GetString(bytesResult.Value!);
        return Result.Success<string>(text);
    }

    /// <summary>
    /// Registers <see cref="IFileStorage"/> implementation with <see cref="LocalFileStorage"/> default,
    /// and <see cref="IFileRegistry"/> with <see cref="DbFileRegistry"/>.
    /// </summary>
    public static IServiceCollection AddFileStorage(this IServiceCollection services)
    {
        services.TryAddSingleton<IFileRegistry, DbFileRegistry>();
        services.TryAddSingleton<IFileStorage, LocalFileStorage>();
        return services;
    }
}
