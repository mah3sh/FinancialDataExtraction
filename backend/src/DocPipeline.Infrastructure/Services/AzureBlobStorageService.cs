using Azure.Identity;
using Azure.Storage.Blobs;
using DocPipeline.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DocPipeline.Infrastructure.Services;

/// <summary>
/// Azure Blob Storage adapter. Activated when Storage:UseAzureBlob=true.
/// Uses a Managed Identity (ManagedIdentityCredential) — no storage account keys needed.
/// </summary>
public class AzureBlobStorageService : IStorageService
{
    private readonly BlobContainerClient _container;
    private readonly ILogger<AzureBlobStorageService> _logger;

    public AzureBlobStorageService(IConfiguration config, ILogger<AzureBlobStorageService> logger)
    {
        _logger = logger;

        var blobEndpoint = config["Storage:BlobEndpoint"]
            ?? throw new InvalidOperationException("Storage:BlobEndpoint is required when Storage:UseAzureBlob=true.");
        var containerName = config["Storage:ContainerName"] ?? "documents";
        var managedIdentityClientId = config["Storage:ManagedIdentityClientId"];

        // Use managed identity client ID when running in Azure (Container Apps)
        // Fall back to DefaultAzureCredential for local dev (uses az login, VS, etc.)
        var credential = string.IsNullOrWhiteSpace(managedIdentityClientId)
            ? new DefaultAzureCredential()
            : (Azure.Core.TokenCredential)new ManagedIdentityCredential(managedIdentityClientId);

        var containerUri = new Uri($"{blobEndpoint.TrimEnd('/')}/{containerName}");
        _container = new BlobContainerClient(containerUri, credential);
    }

    public async Task<string> SaveAsync(Stream fileStream, string fileName, string contentType, CancellationToken ct = default)
    {
        var blobName = $"{Guid.NewGuid()}_{SanitizeFileName(fileName)}";

        _logger.LogInformation("Uploading blob: {BlobName}", blobName);

        var blob = _container.GetBlobClient(blobName);

        // IFormFile streams are non-seekable; the Blob SDK needs seek support for retries
        Stream uploadStream = fileStream.CanSeek
            ? fileStream
            : await BufferToMemoryStreamAsync(fileStream, ct);

        await using (uploadStream)
        {
            await blob.UploadAsync(uploadStream, overwrite: false, cancellationToken: ct);
        }

        // Set content type metadata
        await blob.SetHttpHeadersAsync(new Azure.Storage.Blobs.Models.BlobHttpHeaders
        {
            ContentType = contentType
        }, cancellationToken: ct);

        // Return the blob name — used as the opaque storage path in the Document entity
        return blobName;
    }

    public async Task<Stream> GetAsync(string storagePath, CancellationToken ct = default)
    {
        var blob = _container.GetBlobClient(storagePath);
        var response = await blob.DownloadStreamingAsync(cancellationToken: ct);

        // DownloadStreamingAsync returns a non-seekable stream; PdfPig requires seeking
        var ms = new MemoryStream();
        await response.Value.Content.CopyToAsync(ms, ct);
        ms.Position = 0;
        return ms;
    }

    public async Task DeleteAsync(string storagePath, CancellationToken ct = default)
    {
        var blob = _container.GetBlobClient(storagePath);
        await blob.DeleteIfExistsAsync(cancellationToken: ct);
        _logger.LogInformation("Deleted blob: {BlobName}", storagePath);
    }

    private static string SanitizeFileName(string fileName)
        => Path.GetFileName(fileName).Replace(' ', '_');

    private static async Task<MemoryStream> BufferToMemoryStreamAsync(Stream source, CancellationToken ct)
    {
        var ms = new MemoryStream();
        await source.CopyToAsync(ms, ct);
        ms.Position = 0;
        return ms;
    }
}
