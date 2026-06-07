namespace DocPipeline.Application.Interfaces;

public interface IStorageService
{
    Task<string> SaveAsync(Stream fileStream, string fileName, string contentType, CancellationToken ct = default);
    Task DeleteAsync(string storagePath, CancellationToken ct = default);
    Task<Stream> GetAsync(string storagePath, CancellationToken ct = default);
}
