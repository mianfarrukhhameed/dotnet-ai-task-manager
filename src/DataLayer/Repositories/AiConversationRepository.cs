#nullable enable

using Fistix.TaskManager.Core.Abstractions.Repositories;
using Fistix.TaskManager.Core.DomainModel.Aggregates;
using System.Threading;
using System.Threading.Tasks;

namespace Fistix.TaskManager.DataLayer.Repositories;

public class AiConversationRepository : IAiConversationRepository
{
    private readonly WriteEfContext _context;

    public AiConversationRepository(WriteEfContext context)
    {
        _context = context;
    }

    public async Task AddAsync(AiConversation conversation, CancellationToken cancellationToken)
    {
        _context.AiConversations.Add(conversation);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
