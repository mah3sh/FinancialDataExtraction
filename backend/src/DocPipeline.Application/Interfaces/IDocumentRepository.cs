using DocPipeline.Domain.Entities;
using DocPipeline.Domain.Enums;

namespace DocPipeline.Application.Interfaces;

public interface IDocumentRepository
{
    Task<Document?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Document>> GetByUserIdAsync(string userId, CancellationToken ct = default);
    Task<IReadOnlyList<Document>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Document>> GetByStatusAsync(DocumentStatus status, CancellationToken ct = default);
    Task AddAsync(Document document, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
