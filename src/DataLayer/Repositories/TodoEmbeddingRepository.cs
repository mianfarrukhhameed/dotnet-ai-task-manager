#nullable enable

using Fistix.TaskManager.Core.Abstractions.Repositories;
using Fistix.TaskManager.Core.DomainModel.Aggregates;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Fistix.TaskManager.DataLayer.Repositories;

public class TodoEmbeddingRepository : ITodoEmbeddingRepository
{
    private readonly WriteEfContext _write;
    private readonly ReadEfContext _read;

    public TodoEmbeddingRepository(WriteEfContext write, ReadEfContext read)
    {
        _write = write;
        _read = read;
    }

    public async Task UpsertAsync(int todoId, float[] embedding, string model, CancellationToken cancellationToken)
    {
        var vector = new Vector(embedding);
        var existing = await _write.TodoEmbeddings
            .FirstOrDefaultAsync(e => e.TodoId == todoId && e.EmbeddingModel == model, cancellationToken);

        if (existing is null)
        {
            _write.TodoEmbeddings.Add(new TodoEmbedding
            {
                TodoId = todoId,
                Embedding = vector,
                EmbeddingModel = model,
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.Embedding = vector;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await _write.SaveChangesAsync(cancellationToken);
    }

    public Task<TodoEmbedding?> GetByTodoIdAsync(int todoId, CancellationToken cancellationToken)
    {
        return _write.TodoEmbeddings
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.TodoId == todoId, cancellationToken);
    }

    public async Task<IReadOnlyList<TodoEmbeddingSearchHit>> SearchSimilarAsync(
        float[] queryEmbedding,
        string embeddingModel,
        Guid? ownerExternalId,
        int limit,
        CancellationToken cancellationToken,
        IReadOnlyCollection<Guid>? allowedExternalIds = null)
    {
        if (allowedExternalIds is { Count: 0 })
        {
            return Array.Empty<TodoEmbeddingSearchHit>();
        }

        var queryVector = new Vector(queryEmbedding);
        var query = _read.TodoEmbeddings
            .Where(e => e.EmbeddingModel == embeddingModel);

        if (ownerExternalId.HasValue)
        {
            query = query.Where(e => e.TodoTask != null && e.TodoTask.CreatedByUserId == ownerExternalId.Value);
        }

        if (allowedExternalIds is { Count: > 0 })
        {
            var allowed = allowedExternalIds.ToList();
            query = query.Where(e => e.TodoTask != null && allowed.Contains(e.TodoTask.ExternalId));
        }

        var hits = await query
            .OrderBy(e => e.Embedding.CosineDistance(queryVector))
            .Take(limit)
            .Select(e => new
            {
                e.TodoId,
                TodoExternalId = e.TodoTask!.ExternalId,
                Distance = e.Embedding.CosineDistance(queryVector)
            })
            .ToListAsync(cancellationToken);

        return hits
            .Select(h => new TodoEmbeddingSearchHit(h.TodoExternalId, h.TodoId, h.Distance))
            .ToList();
    }

    public async Task<IReadOnlyList<Guid>> GetTodoExternalIdsMissingEmbeddingsAsync(
        string embeddingModel,
        CancellationToken cancellationToken)
    {
        // Backfill runs on write side (worker).
        var embeddedTodoIds = _write.TodoEmbeddings
            .Where(e => e.EmbeddingModel == embeddingModel)
            .Select(e => e.TodoId);

        return await _write.TodoTasks
            .AsNoTracking()
            .Where(t => !embeddedTodoIds.Contains(t.Id))
            .Select(t => t.ExternalId)
            .ToListAsync(cancellationToken);
    }
}
