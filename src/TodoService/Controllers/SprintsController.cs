#nullable enable

using Fistix.TaskManager.Core.Exceptions;
using Fistix.TaskManager.ViewModel.Dtos;
using Fistix.TaskManager.ViewModel.Queries.Todos;

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Fistix.TaskManager.TodoService.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class SprintsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<SprintsController> _logger;

    public SprintsController(IMediator mediator, ILogger<SprintsController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<SprintDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSprints()
    {
        try
        {
            var result = await _mediator.Send(new GetSprintsQuery());
            return Ok(result.Payload);
        }
        catch (ForbiddenAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing sprints");
            return Fistix.TaskManager.ApiHost.Shared.Extensions.ApiErrorResponses.UnexpectedError(HttpContext, "Failed to list sprints");
        }
    }

    [HttpGet("{externalId:guid}")]
    [ProducesResponseType(typeof(SprintDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSprint(Guid externalId)
    {
        try
        {
            var result = await _mediator.Send(new GetSprintQuery { ExternalId = externalId });
            return Ok(result.Payload);
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
        catch (ForbiddenAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading sprint {SprintExternalId}", externalId);
            return Fistix.TaskManager.ApiHost.Shared.Extensions.ApiErrorResponses.UnexpectedError(HttpContext, "Failed to load sprint");
        }
    }
}
