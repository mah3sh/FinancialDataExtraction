namespace DocPipeline.Application.Interfaces;

public interface IDocumentProcessingQueue
{
    void Enqueue(Guid documentId);
    ValueTask<Guid> DequeueAsync(CancellationToken ct);
}
