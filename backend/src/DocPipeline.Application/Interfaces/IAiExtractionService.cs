namespace DocPipeline.Application.Interfaces;

public interface IAiExtractionService
{
    /// <summary>
    /// Extracts structured financial data from a document stream.
    /// Returns a dynamic JSON string — schema is determined by AI.
    /// </summary>
    Task<string> ExtractAsync(Stream fileStream, string contentType, CancellationToken ct = default);
}
