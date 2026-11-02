#nullable enable

using Fistix.TaskManager.Core.Abstractions.Repositories;
using Fistix.TaskManager.Core.DomainModel.Aggregates;
using System.Threading;
using System.Threading.Tasks;

namespace Fistix.TaskManager.DataLayer.Repositories;

public class ToolExecutionLogRepository : IToolExecutionLogRepository
{
    private readonly WriteEfContext _context;

    public ToolExecutionLogRepository(WriteEfContext context)
    {
        _context = context;
    }

    public async Task AddAsync(ToolExecutionLog log, CancellationToken cancellationToken)
    {
        _context.ToolExecutionLogs.Add(log);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
