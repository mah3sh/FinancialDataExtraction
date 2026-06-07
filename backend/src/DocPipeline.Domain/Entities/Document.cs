using DocPipeline.Domain.Enums;

namespace DocPipeline.Domain.Entities;

public class Document
{
    public Guid Id { get; private set; }
    public string FileName { get; private set; } = default!;
    public string ContentType { get; private set; } = default!;
    public long FileSizeBytes { get; private set; }
    public string StoragePath { get; private set; } = default!;
    public DocumentStatus Status { get; private set; }
    public string? ExtractedDataJson { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string UploadedByUserId { get; private set; } = default!;
    public DateTime UploadedAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }

    // Optimistic concurrency token
    public byte[] RowVersion { get; private set; } = default!;

    private Document() { } // EF Core

    public static Document Create(
        string fileName,
        string contentType,
        long fileSizeBytes,
        string storagePath,
        string uploadedByUserId)
    {
        if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("File name is required.", nameof(fileName));
        if (string.IsNullOrWhiteSpace(storagePath)) throw new ArgumentException("Storage path is required.", nameof(storagePath));
        if (string.IsNullOrWhiteSpace(uploadedByUserId)) throw new ArgumentException("User ID is required.", nameof(uploadedByUserId));
        if (fileSizeBytes <= 0) throw new ArgumentOutOfRangeException(nameof(fileSizeBytes), "File size must be positive.");

        return new Document
        {
            Id = Guid.NewGuid(),
            FileName = fileName,
            ContentType = contentType,
            FileSizeBytes = fileSizeBytes,
            StoragePath = storagePath,
            Status = DocumentStatus.Pending,
            UploadedByUserId = uploadedByUserId,
            UploadedAt = DateTime.UtcNow
        };
    }

    public void MarkProcessing()
    {
        if (Status != DocumentStatus.Pending)
            throw new InvalidOperationException($"Cannot transition to Processing from {Status}.");
        Status = DocumentStatus.Processing;
    }

    public void MarkCompleted(string extractedDataJson)
    {
        if (Status != DocumentStatus.Processing)
            throw new InvalidOperationException($"Cannot transition to Completed from {Status}.");
        if (string.IsNullOrWhiteSpace(extractedDataJson))
            throw new ArgumentException("Extracted data cannot be empty.", nameof(extractedDataJson));

        Status = DocumentStatus.Completed;
        ExtractedDataJson = extractedDataJson;
        ProcessedAt = DateTime.UtcNow;
        ErrorMessage = null;
    }

    public void MarkFailed(string errorMessage)
    {
        Status = DocumentStatus.Failed;
        ErrorMessage = errorMessage;
        ProcessedAt = DateTime.UtcNow;
    }

    public void ResetToPending()
    {
        if (Status != DocumentStatus.Failed)
            throw new InvalidOperationException("Only failed documents can be retried.");
        Status = DocumentStatus.Pending;
        ErrorMessage = null;
    }
}
