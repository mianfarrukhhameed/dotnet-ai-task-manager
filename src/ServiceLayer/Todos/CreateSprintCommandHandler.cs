#nullable enable

using Fistix.TaskManager.Core.Abstractions.Repositories;
using Fistix.TaskManager.Core.Abstractions.Services;
using Fistix.TaskManager.Core.DomainModel.Aggregates;
using Fistix.TaskManager.Core.SecurityModel;
using Fistix.TaskManager.ViewModel.Commands.Todos;
using Fistix.TaskManager.ViewModel.Dtos;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Fistix.TaskManager.ServiceLayer.Todos;

public class CreateSprintCommandHandler : IRequestHandler<CreateSprintCommand, CreateSprintCommandResult>
{
    private readonly ISprintRepository _sprintRepository;
    private readonly ITodoTaskRepository _todoTaskRepository;
    private readonly ICurrentUserService _currentUserService;

    public CreateSprintCommandHandler(
        ISprintRepository sprintRepository,
        ITodoTaskRepository todoTaskRepository,
        ICurrentUserService currentUserService)
    {
        _sprintRepository = sprintRepository;
        _todoTaskRepository = todoTaskRepository;
        _currentUserService = currentUserService;
    }

    public async Task<CreateSprintCommandResult> Handle(CreateSprintCommand command, CancellationToken cancellationToken)
    {
        var userId = TodoAccessGuard.RequireCurrentUserId(_currentUserService);
        var todos = await ResolveOwnedTodos(command.TodoExternalIds, cancellationToken);

        var sprint = new Sprint
        {
            Name = command.Name.Trim(),
            StartDate = command.StartDate,
            EndDate = command.EndDate,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            Reasoning = command.Reasoning
        };
        sprint.GenerateNewExternalId();

        foreach (var todo in todos)
        {
            sprint.SprintTodos.Add(new SprintTodo { TodoId = todo.Id });
        }

        await _sprintRepository.Create(sprint, cancellationToken);

        // Map from in-memory graph (todos already loaded for authz) — no post-create re-query.
        return new CreateSprintCommandResult
        {
            Payload = MapSprint(sprint, todos)
        };
    }

    private async Task<List<TodoTask>> ResolveOwnedTodos(
        List<Guid> todoExternalIds,
        CancellationToken cancellationToken)
    {
        var todos = new List<TodoTask>();
        foreach (var id in todoExternalIds.Distinct())
        {
            var todo = await _todoTaskRepository.Get(id, cancellationToken);
            if (!_currentUserService.HasAdminProfile)
            {
                TodoAccessGuard.EnsureCanAccess(todo, _currentUserService);
            }

            todos.Add(todo);
        }

        return todos;
    }

    internal static SprintDto MapSprint(Sprint sprint, IReadOnlyList<TodoTask>? todos = null)
    {
        var taskEntities = todos
            ?? sprint.SprintTodos
                .Where(st => st.TodoTask is not null)
                .Select(st => st.TodoTask!)
                .ToList();

        var tasks = taskEntities
            .Select(t => new SprintTaskSummaryDto
            {
                ExternalId = t.ExternalId,
                Title = t.Title,
                Priority = t.Priority,
                Status = t.Status,
                DueDate = t.DueDate,
                Category = t.Category
            })
            .ToList();

        return new SprintDto
        {
            ExternalId = sprint.ExternalId,
            Name = sprint.Name,
            StartDate = sprint.StartDate,
            EndDate = sprint.EndDate,
            CreatedAt = sprint.CreatedAt,
            Reasoning = sprint.Reasoning,
            TodoExternalIds = tasks.Select(t => t.ExternalId).ToList(),
            Tasks = tasks
        };
    }
}
