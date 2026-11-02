#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fistix.TaskManager.Core.Abstractions.Repositories;
using Fistix.TaskManager.Core.DomainModel.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace Fistix.TaskManager.DataLayer.Repositories;

public sealed class KnowledgeDocumentRepository : IKnowledgeDocumentRepository
{
    private readonly WriteEfContext _write;
    private readonly ReadEfContext _read;

    public KnowledgeDocumentRepository(WriteEfContext write, ReadEfContext read)
    {
        _write = write;
        _read = read;
    }

    public async Task CreateAsync(KnowledgeDocument document, CancellationToken cancellationToken)
    {
        _write.KnowledgeDocuments.Add(document);
        await _write.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(KnowledgeDocument document, CancellationToken cancellationToken)
    {
        document.UpdatedAt = DateTime.UtcNow;
        _write.KnowledgeDocuments.Update(document);
        await _write.SaveChangesAsync(cancellationToken);
    }

    public Task<KnowledgeDocument?> GetByExternalIdAsync(Guid externalId, CancellationToken cancellationToken) =>
        // Authz / mutate paths → write.
        _write.KnowledgeDocuments.FirstOrDefaultAsync(d => d.ExternalId == externalId, cancellationToken);

    public Task<KnowledgeDocument?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        _write.KnowledgeDocuments.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public async Task<IReadOnlyList<KnowledgeDocument>> ListByOwnerAsync(
        Guid ownerExternalId,
        CancellationToken cancellationToken)
    {
        return await _read.KnowledgeDocuments
            .Where(d => d.CreatedByUserId == ownerExternalId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<KnowledgeDocument?> FindByOwnerAndFileNameAsync(
        Guid ownerExternalId,
        string fileName,
        CancellationToken cancellationToken)
    {
        var normalized = fileName.Trim();
        return _write.KnowledgeDocuments.FirstOrDefaultAsync(
            d => d.CreatedByUserId == ownerExternalId
                 && d.FileName.ToLower() == normalized.ToLower(),
            cancellationToken);
    }

    public async Task DeleteAsync(KnowledgeDocument document, CancellationToken cancellationToken)
    {
        _write.KnowledgeDocuments.Remove(document);
        await _write.SaveChangesAsync(cancellationToken);
    }
}

