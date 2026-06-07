using DocPipeline.Application.DTOs;
using DocPipeline.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DocPipeline.API.Controllers;

[ApiController]
[Route("api/documents")]
[Authorize]
[Produces("application/json")]
public class DocumentsController(DocumentService documentService) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException("User ID claim missing.");
    private string Role => User.FindFirstValue(ClaimTypes.Role) ?? "Uploader";

    /// <summary>Upload a financial document for AI extraction.</summary>
    [HttpPost]
    [Authorize(Roles = "Uploader")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    [ProducesResponseType(typeof(DocumentSummaryDto), 202)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { detail = "No file provided." });

        var request = new UploadDocumentRequest(
            file.FileName,
            file.ContentType,
            file.Length,
            file.OpenReadStream());

        var result = await documentService.UploadAsync(request, UserId, ct);
        return Accepted($"/api/documents/{result.Id}/status", result);
    }

    /// <summary>List documents. Reviewers see all; Uploaders see their own.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DocumentSummaryDto>), 200)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var docs = await documentService.ListAsync(UserId, Role, ct);
        return Ok(docs);
    }

    /// <summary>Get document status (for polling).</summary>
    [HttpGet("{id:guid}/status")]
    [ProducesResponseType(typeof(DocumentSummaryDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetStatus(Guid id, CancellationToken ct)
    {
        var doc = await documentService.GetSummaryAsync(id, UserId, Role, ct);
        return Ok(doc);
    }

    /// <summary>Get full extraction result once processing is complete.</summary>
    [HttpGet("{id:guid}/result")]
    [ProducesResponseType(typeof(DocumentResultDto), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> GetResult(Guid id, CancellationToken ct)
    {
        var result = await documentService.GetResultAsync(id, UserId, Role, ct);

        if (result.Status == "Pending" || result.Status == "Processing")
            return Conflict(new { detail = $"Document is still {result.Status}. Poll /status until Completed or Failed." });

        return Ok(result);
    }

    /// <summary>Retry a failed document.</summary>
    [HttpPost("{id:guid}/retry")]
    [Authorize(Roles = "Uploader,Reviewer")]
    [ProducesResponseType(typeof(RetryDocumentResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Retry(Guid id, CancellationToken ct)
    {
        var result = await documentService.RetryAsync(id, UserId, Role, ct);
        return Ok(result);
    }
}
