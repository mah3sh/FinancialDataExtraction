using DocPipeline.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace DocPipeline.Infrastructure.Services;

/// <summary>
/// Local filesystem storage. Swap with AzureBlobStorageService for production.
/// </summary>
public class LocalStorageService(IConfiguration config) : IStorageService
{
    private readonly string _basePath = config["Storage:LocalBasePath"]
        ?? Path.Combine(Path.GetTempPath(), "docpipeline-uploads");

    public async Task<string> SaveAsync(Stream fileStream, string fileName, string contentType, CancellationToken ct = default)
    {
        Directory.CreateDirectory(_basePath);

        var safeFileName = $"{Guid.NewGuid()}_{Path.GetFileName(fileName)}";
        var fullPath = Path.Combine(_basePath, safeFileName);

        await using var fs = File.Create(fullPath);
        await fileStream.CopyToAsync(fs, ct);

        return fullPath;
    }

    public Task DeleteAsync(string storagePath, CancellationToken ct = default)
    {
        if (File.Exists(storagePath))
            File.Delete(storagePath);
        return Task.CompletedTask;
    }

    public Task<Stream> GetAsync(string storagePath, CancellationToken ct = default)
    {
        if (!File.Exists(storagePath))
            throw new FileNotFoundException("File not found.", storagePath);
        return Task.FromResult<Stream>(File.OpenRead(storagePath));
    }
}
