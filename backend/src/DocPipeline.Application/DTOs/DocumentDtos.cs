using DocPipeline.Domain.Enums;
using System.Text.Json;

namespace DocPipeline.Application.DTOs;

public record UploadDocumentRequest(
    string FileName,
    string ContentType,
    long FileSizeBytes,
    Stream FileStream
);

public record DocumentSummaryDto(
    Guid Id,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    string Status,
    string UploadedByUserId,
    DateTime UploadedAt,
    DateTime? ProcessedAt,
    string? ErrorMessage
);

public record DocumentResultDto(
    Guid Id,
    string FileName,
    string Status,
    DateTime UploadedAt,
    DateTime? ProcessedAt,
    JsonElement? ExtractedData,
    string? ErrorMessage
);

public record RetryDocumentResponse(Guid Id, string Status);

public record RegisterRequest(string Email, string Password, string Role);

public record LoginRequest(string Email, string Password);

public record AuthResponse(string Token, string Email, string Role, DateTime ExpiresAt);
