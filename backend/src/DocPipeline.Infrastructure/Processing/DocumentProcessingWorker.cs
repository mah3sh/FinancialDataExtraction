using DocPipeline.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DocPipeline.Infrastructure.Data;

namespace DocPipeline.Infrastructure.Processing;

public class DocumentProcessingWorker(
    IDocumentProcessingQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<DocumentProcessingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Document processing worker started.");

        await foreach (var documentId in DequeueAllAsync(stoppingToken))
        {
            await ProcessDocumentAsync(documentId, stoppingToken);
        }

        logger.LogInformation("Document processing worker stopped.");
    }

    private async IAsyncEnumerable<Guid> DequeueAllAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            Guid id;
            try { id = await queue.DequeueAsync(ct); }
            catch (OperationCanceledException) { yield break; }
            yield return id;
        }
    }

    private async Task ProcessDocumentAsync(Guid documentId, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var extraction = scope.ServiceProvider.GetRequiredService<IAiExtractionService>();
        var storage = scope.ServiceProvider.GetRequiredService<IStorageService>();

        var document = await db.Documents.FirstOrDefaultAsync(d => d.Id == documentId, ct);
        if (document is null)
        {
            logger.LogWarning("Document {Id} not found; skipping.", documentId);
            return;
        }

        logger.LogInformation("Processing document {Id} ({FileName}).", documentId, document.FileName);

        try
        {
            document.MarkProcessing();
            await db.SaveChangesAsync(ct);

            // Stream from storage (works for both local filesystem and Azure Blob)
            await using var fileStream = await storage.GetAsync(document.StoragePath, ct);
            var json = await extraction.ExtractAsync(fileStream, document.ContentType, ct);

            document.MarkCompleted(json);
            await db.SaveChangesAsync(ct);

            logger.LogInformation("Document {Id} completed successfully.", documentId);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Another worker instance picked up the same document — safe to skip
            logger.LogWarning(ex, "Concurrency conflict on document {Id}; skipping.", documentId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process document {Id}.", documentId);
            try
            {
                document.MarkFailed(ex.Message.Length > 1900 ? ex.Message[..1900] : ex.Message);
                await db.SaveChangesAsync(ct);
            }
            catch (Exception saveEx)
            {
                logger.LogError(saveEx, "Failed to persist failure state for document {Id}.", documentId);
            }
        }
    }
}
