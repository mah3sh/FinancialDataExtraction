using DocPipeline.Application.DTOs;
using DocPipeline.Application.Exceptions;
using DocPipeline.Application.Interfaces;
using DocPipeline.Domain.Entities;
using DocPipeline.Domain.Enums;
using System.Text.Json;

namespace DocPipeline.Application.Services;

public class DocumentService(
    IDocumentRepository repository,
    IStorageService storage,
    IDocumentProcessingQueue queue)
{
    private static readonly string[] AllowedContentTypes =
        ["application/pdf", "image/png", "image/jpeg", "image/jpg", "image/webp"];

    private const long MaxFileSizeBytes = 20 * 1024 * 1024; // 20 MB

    public async Task<DocumentSummaryDto> UploadAsync(
        UploadDocumentRequest request,
        string userId,
        CancellationToken ct = default)
    {
        ValidateUpload(request);

        var storagePath = await storage.SaveAsync(
            request.FileStream, request.FileName, request.ContentType, ct);

        var document = Document.Create(
            request.FileName,
            request.ContentType,
            request.FileSizeBytes,
            storagePath,
            userId);

        await repository.AddAsync(document, ct);
        await repository.SaveChangesAsync(ct);

        queue.Enqueue(document.Id);

        return ToSummary(document);
    }

    public async Task<DocumentSummaryDto> GetSummaryAsync(Guid id, string userId, string role, CancellationToken ct = default)
    {
        var doc = await GetAndAuthorizeAsync(id, userId, role, ct);
        return ToSummary(doc);
    }

    public async Task<DocumentResultDto> GetResultAsync(Guid id, string userId, string role, CancellationToken ct = default)
    {
        var doc = await GetAndAuthorizeAsync(id, userId, role, ct);

        JsonElement? extracted = null;
        if (doc.ExtractedDataJson is not null)
        {
            try { extracted = JsonSerializer.Deserialize<JsonElement>(doc.ExtractedDataJson); }
            catch { /* malformed JSON stored — return null gracefully */ }
        }

        return new DocumentResultDto(
            doc.Id,
            doc.FileName,
            doc.Status.ToString(),
            doc.UploadedAt,
            doc.ProcessedAt,
            extracted,
            doc.ErrorMessage);
    }

    public async Task<IReadOnlyList<DocumentSummaryDto>> ListAsync(string userId, string role, CancellationToken ct = default)
    {
        var docs = role == "Reviewer"
            ? await repository.GetAllAsync(ct)
            : await repository.GetByUserIdAsync(userId, ct);

        return docs.Select(ToSummary).ToList();
    }

    public async Task<RetryDocumentResponse> RetryAsync(Guid id, string userId, string role, CancellationToken ct = default)
    {
        var doc = await GetAndAuthorizeAsync(id, userId, role, ct);

        if (doc.Status != DocumentStatus.Failed)
            throw new ValidationException("Only failed documents can be retried.");

        doc.ResetToPending();
        await repository.SaveChangesAsync(ct);

        queue.Enqueue(doc.Id);
        return new RetryDocumentResponse(doc.Id, doc.Status.ToString());
    }

    private async Task<Document> GetAndAuthorizeAsync(Guid id, string userId, string role, CancellationToken ct)
    {
        var doc = await repository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Document), id);

        if (role != "Reviewer" && doc.UploadedByUserId != userId)
            throw new UnauthorizedException("You do not have access to this document.");

        return doc;
    }

    private static void ValidateUpload(UploadDocumentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FileName))
            throw new ValidationException("File name is required.");

        if (!AllowedContentTypes.Contains(request.ContentType, StringComparer.OrdinalIgnoreCase))
            throw new ValidationException($"Content type '{request.ContentType}' is not supported. Allowed: PDF, PNG, JPEG, WebP.");

        if (request.FileSizeBytes > MaxFileSizeBytes)
            throw new ValidationException($"File exceeds maximum size of {MaxFileSizeBytes / 1024 / 1024} MB.");

        if (request.FileSizeBytes <= 0)
            throw new ValidationException("File is empty.");
    }

    private static DocumentSummaryDto ToSummary(Document doc) => new(
        doc.Id,
        doc.FileName,
        doc.ContentType,
        doc.FileSizeBytes,
        doc.Status.ToString(),
        doc.UploadedByUserId,
        doc.UploadedAt,
        doc.ProcessedAt,
        doc.ErrorMessage);
}
