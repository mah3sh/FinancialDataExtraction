using DocPipeline.Application.DTOs;
using DocPipeline.Application.Exceptions;
using DocPipeline.Application.Interfaces;
using DocPipeline.Application.Services;
using DocPipeline.Domain.Entities;
using DocPipeline.Domain.Enums;
using FluentAssertions;
using Moq;

namespace DocPipeline.Tests.Application;

public class DocumentServiceTests
{
    private readonly Mock<IDocumentRepository> _repo = new();
    private readonly Mock<IStorageService> _storage = new();
    private readonly Mock<IDocumentProcessingQueue> _queue = new();
    private readonly DocumentService _sut;

    public DocumentServiceTests()
    {
        _sut = new DocumentService(_repo.Object, _storage.Object, _queue.Object);
    }

    [Fact]
    public async Task UploadAsync_ValidPdf_SavesAndEnqueues()
    {
        _storage.Setup(s => s.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync("/tmp/test.pdf");
        _repo.Setup(r => r.AddAsync(It.IsAny<Document>(), default)).Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync(default)).Returns(Task.CompletedTask);

        var request = new UploadDocumentRequest("invoice.pdf", "application/pdf", 1024, new MemoryStream([1, 2, 3]));
        var result = await _sut.UploadAsync(request, "user-1");

        result.Status.Should().Be("Pending");
        result.FileName.Should().Be("invoice.pdf");
        _queue.Verify(q => q.Enqueue(It.IsAny<Guid>()), Times.Once);
        _storage.Verify(s => s.SaveAsync(It.IsAny<Stream>(), "invoice.pdf", "application/pdf", default), Times.Once);
    }

    [Fact]
    public async Task UploadAsync_UnsupportedContentType_ThrowsValidationException()
    {
        var request = new UploadDocumentRequest("file.exe", "application/octet-stream", 100, new MemoryStream([1]));
        var act = () => _sut.UploadAsync(request, "user-1");
        await act.Should().ThrowAsync<ValidationException>().WithMessage("*not supported*");
    }

    [Fact]
    public async Task UploadAsync_FileTooLarge_ThrowsValidationException()
    {
        var request = new UploadDocumentRequest("big.pdf", "application/pdf",
            21 * 1024 * 1024, new MemoryStream([1]));
        var act = () => _sut.UploadAsync(request, "user-1");
        await act.Should().ThrowAsync<ValidationException>().WithMessage("*exceeds maximum*");
    }

    [Fact]
    public async Task GetSummaryAsync_NonOwnerUploader_ThrowsUnauthorizedException()
    {
        var doc = Document.Create("f.pdf", "application/pdf", 100, "/p", "owner-user");
        _repo.Setup(r => r.GetByIdAsync(doc.Id, default)).ReturnsAsync(doc);

        var act = () => _sut.GetSummaryAsync(doc.Id, "other-user", "Uploader");
        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task GetSummaryAsync_Reviewer_CanAccessAnyDocument()
    {
        var doc = Document.Create("f.pdf", "application/pdf", 100, "/p", "owner-user");
        _repo.Setup(r => r.GetByIdAsync(doc.Id, default)).ReturnsAsync(doc);

        var result = await _sut.GetSummaryAsync(doc.Id, "reviewer-user", "Reviewer");
        result.Id.Should().Be(doc.Id);
    }

    [Fact]
    public async Task GetSummaryAsync_NotFound_ThrowsNotFoundException()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((Document?)null);
        var act = () => _sut.GetSummaryAsync(Guid.NewGuid(), "user-1", "Uploader");
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task RetryAsync_CompletedDocument_ThrowsValidationException()
    {
        var doc = Document.Create("f.pdf", "application/pdf", 100, "/p", "user-1");
        doc.MarkProcessing();
        doc.MarkCompleted("{\"ok\":true}");
        _repo.Setup(r => r.GetByIdAsync(doc.Id, default)).ReturnsAsync(doc);

        var act = () => _sut.RetryAsync(doc.Id, "user-1", "Uploader");
        await act.Should().ThrowAsync<ValidationException>().WithMessage("*failed documents*");
    }
}
