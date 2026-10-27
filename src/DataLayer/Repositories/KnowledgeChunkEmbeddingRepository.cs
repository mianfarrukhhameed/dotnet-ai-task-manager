#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fistix.TaskManager.Core.Abstractions.Repositories;
using Fistix.TaskManager.Core.DomainModel.Aggregates;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace Fistix.TaskManager.DataLayer.Repositories;

public sealed class KnowledgeChunkEmbeddingRepository : IKnowledgeChunkEmbeddingRepository
{
    private readonly WriteEfContext _write;
    private readonly ReadEfContext _read;

    public KnowledgeChunkEmbeddingRepository(WriteEfContext write, ReadEfContext read)
    {
        _write = write;
        _read = read;
    }

    public async Task UpsertAsync(int chunkId, float[] embedding, string model, CancellationToken cancellationToken)
    {
        var vector = new Vector(embedding);
        var existing = await _write.KnowledgeChunkEmbeddings
            .FirstOrDefaultAsync(e => e.ChunkId == chunkId && e.EmbeddingModel == model, cancellationToken);

        if (existing is null)
        {
            _write.KnowledgeChunkEmbeddings.Add(new KnowledgeChunkEmbedding
            {
                ChunkId = chunkId,
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

    public async Task<IReadOnlyList<KnowledgeChunkSearchHit>> SearchSimilarAsync(
        float[] queryEmbedding,
        string embeddingModel,
        Guid ownerExternalId,
        int limit,
        CancellationToken cancellationToken,
        Guid? documentExternalId = null,
        IReadOnlyCollection<Guid>? excludeChunkExternalIds = null)
    {
        var queryVector = new Vector(queryEmbedding);
        var query = _read.KnowledgeChunkEmbeddings
            .Where(e => e.EmbeddingModel == embeddingModel
                        && e.Chunk != null
                        && e.Chunk.Document != null
                        && e.Chunk.Document.CreatedByUserId == ownerExternalId);

        if (documentExternalId.HasValue)
        {
            query = query.Where(e => e.Chunk!.Document!.ExternalId == documentExternalId.Value);
        }

        if (excludeChunkExternalIds is { Count: > 0 })
        {
            var exclude = excludeChunkExternalIds.ToArray();
            query = query.Where(e => !exclude.Contains(e.Chunk!.ExternalId));
        }

        var hits = await query
            .OrderBy(e => e.Embedding.CosineDistance(queryVector))
            .Take(limit)
            .Select(e => new
            {
                e.ChunkId,
                ChunkExternalId = e.Chunk!.ExternalId,
                DocumentExternalId = e.Chunk.Document!.ExternalId,
                FileName = e.Chunk.Document.FileName,
                e.Chunk.Ordinal,
                e.Chunk.Content,
                e.Chunk.Heading,
                Distance = e.Embedding.CosineDistance(queryVector)
            })
            .ToListAsync(cancellationToken);

        return hits
            .Select(h => new KnowledgeChunkSearchHit(
                h.ChunkExternalId,
                h.ChunkId,
                h.DocumentExternalId,
                h.FileName,
                h.Ordinal,
                h.Content,
                h.Heading,
                h.Distance))
            .ToList();
    }
}
