using DocPipeline.Domain.Entities;
using DocPipeline.Domain.Enums;
using FluentAssertions;

namespace DocPipeline.Tests.Domain;

public class DocumentTests
{
    private static Document MakeDoc() => Document.Create(
        "invoice.pdf", "application/pdf", 1024, "/tmp/invoice.pdf", "user-1");

    [Fact]
    public void Create_WithValidArgs_SetsPendingStatus()
    {
        var doc = MakeDoc();
        doc.Status.Should().Be(DocumentStatus.Pending);
        doc.Id.Should().NotBeEmpty();
        doc.UploadedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void MarkProcessing_FromPending_TransitionsToProcessing()
    {
        var doc = MakeDoc();
        doc.MarkProcessing();
        doc.Status.Should().Be(DocumentStatus.Processing);
    }

    [Fact]
    public void MarkProcessing_FromCompleted_Throws()
    {
        var doc = MakeDoc();
        doc.MarkProcessing();
        doc.MarkCompleted("{\"documentType\":\"Invoice\"}");

        var act = () => doc.MarkProcessing();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot transition to Processing*");
    }

    [Fact]
    public void MarkCompleted_SetsJsonAndProcessedAt()
    {
        const string json = "{\"documentType\":\"Invoice\",\"totalAmount\":500}";
        var doc = MakeDoc();
        doc.MarkProcessing();
        doc.MarkCompleted(json);

        doc.Status.Should().Be(DocumentStatus.Completed);
        doc.ExtractedDataJson.Should().Be(json);
        doc.ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkCompleted_WithEmptyJson_Throws()
    {
        var doc = MakeDoc();
        doc.MarkProcessing();
        var act = () => doc.MarkCompleted(string.Empty);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkFailed_SetsFailedStatus_AndMessage()
    {
        var doc = MakeDoc();
        doc.MarkProcessing();
        doc.MarkFailed("Azure OpenAI timeout");

        doc.Status.Should().Be(DocumentStatus.Failed);
        doc.ErrorMessage.Should().Be("Azure OpenAI timeout");
    }

    [Fact]
    public void ResetToPending_FromFailed_AllowsRetry()
    {
        var doc = MakeDoc();
        doc.MarkProcessing();
        doc.MarkFailed("error");
        doc.ResetToPending();

        doc.Status.Should().Be(DocumentStatus.Pending);
        doc.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void ResetToPending_FromCompleted_Throws()
    {
        var doc = MakeDoc();
        doc.MarkProcessing();
        doc.MarkCompleted("{\"ok\":true}");

        var act = () => doc.ResetToPending();
        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("", "application/pdf", 1024, "/p", "u")]
    [InlineData("f.pdf", "application/pdf", 0, "/p", "u")]
    [InlineData("f.pdf", "application/pdf", -1, "/p", "u")]
    [InlineData("f.pdf", "application/pdf", 1024, "", "u")]
    [InlineData("f.pdf", "application/pdf", 1024, "/p", "")]
    public void Create_WithInvalidArgs_Throws(
        string name, string ct, long size, string path, string userId)
    {
        var act = () => Document.Create(name, ct, size, path, userId);
        act.Should().Throw<Exception>();
    }
}
