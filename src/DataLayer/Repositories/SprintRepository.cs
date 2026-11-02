#nullable enable

using Fistix.TaskManager.Core.Abstractions.Repositories;
using Fistix.TaskManager.Core.DomainModel.Aggregates;
using Fistix.TaskManager.Core.Exceptions;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Fistix.TaskManager.DataLayer.Repositories;

public class SprintRepository : ISprintRepository
{
    private readonly WriteEfContext _write;
    private readonly ReadEfContext _read;

    public SprintRepository(WriteEfContext write, ReadEfContext read)
    {
        _write = write;
        _read = read;
    }

    public async Task<bool> Create(Sprint sprint, CancellationToken cancellationToken)
    {
        _write.Sprints.Add(sprint);
        var effectedRecords = await _write.SaveChangesAsync(cancellationToken);
        return effectedRecords > 0;
    }

    public async Task<Sprint> Get(Guid externalId, CancellationToken cancellationToken)
    {
        // Query display → read (fallback = primary when MainDbRead unset).
        var entity = await _read.Sprints
            .Include(s => s.SprintTodos)
            .ThenInclude(st => st.TodoTask)
            .FirstOrDefaultAsync(s => s.ExternalId == externalId, cancellationToken);

        if (entity is null)
        {
            throw new NotFoundException();
        }

        return entity;
    }

    public async Task<List<Sprint>> GetByOwner(Guid ownerExternalId, CancellationToken cancellationToken)
    {
        return await _read.Sprints
            .Include(s => s.SprintTodos)
            .ThenInclude(st => st.TodoTask)
            .Where(s => s.CreatedByUserId == ownerExternalId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
