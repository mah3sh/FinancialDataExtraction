using DocPipeline.Application.Interfaces;
using DocPipeline.Domain.Entities;
using DocPipeline.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DocPipeline.Infrastructure.Data.Repositories;

public class DocumentRepository(AppDbContext db) : IDocumentRepository
{
    public Task<Document?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Documents.FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<IReadOnlyList<Document>> GetByUserIdAsync(string userId, CancellationToken ct = default)
        => await db.Documents
            .Where(d => d.UploadedByUserId == userId)
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Document>> GetAllAsync(CancellationToken ct = default)
        => await db.Documents
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Document>> GetByStatusAsync(DocumentStatus status, CancellationToken ct = default)
        => await db.Documents
            .Where(d => d.Status == status)
            .ToListAsync(ct);

    public Task AddAsync(Document document, CancellationToken ct = default)
    {
        db.Documents.Add(document);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);
}
