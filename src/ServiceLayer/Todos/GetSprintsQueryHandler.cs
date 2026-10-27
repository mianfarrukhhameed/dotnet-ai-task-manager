#nullable enable

using Fistix.TaskManager.Core.Abstractions.Repositories;
using Fistix.TaskManager.Core.Abstractions.Services;
using Fistix.TaskManager.Core.SecurityModel;
using Fistix.TaskManager.ViewModel.Queries.Todos;
using MediatR;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Fistix.TaskManager.ServiceLayer.Todos;

public class GetSprintsQueryHandler : IRequestHandler<GetSprintsQuery, GetSprintsQueryResult>
{
    private readonly ISprintRepository _sprintRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetSprintsQueryHandler(
        ISprintRepository sprintRepository,
        ICurrentUserService currentUserService)
    {
        _sprintRepository = sprintRepository;
        _currentUserService = currentUserService;
    }

    public async Task<GetSprintsQueryResult> Handle(GetSprintsQuery request, CancellationToken cancellationToken)
    {
        var userId = TodoAccessGuard.RequireCurrentUserId(_currentUserService);
        var sprints = await _sprintRepository.GetByOwner(userId, cancellationToken);

        return new GetSprintsQueryResult
        {
            Payload = sprints.Select(s => CreateSprintCommandHandler.MapSprint(s)).ToList()
        };
    }
}
