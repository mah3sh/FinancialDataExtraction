using DocPipeline.Application.Interfaces;
using System.Threading.Channels;

namespace DocPipeline.Infrastructure.Processing;

public class DocumentProcessingQueue : IDocumentProcessingQueue
{
    // Bounded channel prevents unbounded memory growth
    private readonly Channel<Guid> _channel = Channel.CreateBounded<Guid>(
        new BoundedChannelOptions(500)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true
        });

    public void Enqueue(Guid documentId)
    {
        // Fire-and-forget write; channel is bounded so we don't block the upload path
        _channel.Writer.TryWrite(documentId);
    }

    public ValueTask<Guid> DequeueAsync(CancellationToken ct)
        => _channel.Reader.ReadAsync(ct);
}
