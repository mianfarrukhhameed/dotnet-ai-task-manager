using Fistix.TaskManager.Core.Exceptions;
using Fistix.TaskManager.Core.SecurityModel;
using Fistix.TaskManager.AiLayer.Shared;
using Fistix.TaskManager.ViewModel.Commands.Todos;
using Fistix.TaskManager.ViewModel.Dtos;
using Fistix.TaskManager.ViewModel.Queries.Todos;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Fistix.TaskManager.AiJobsService.Controllers;

/// <summary>
/// API endpoints for AI features (summarization, classification, etc).
/// </summary>
[ApiController]
[Route("api/ai")]
[Authorize]
public class AiJobsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<AiJobsController> _logger;

    public AiJobsController(IMediator mediator, ILogger<AiJobsController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Starts an async sprint optimizer job. Progress is pushed over SignalR; poll get/active for status.
    /// </summary>
    [HttpPost("agent/sprint-optimizer")]
    [EnableRateLimiting(RateLimitPolicies.AiAgents)]
    [ProducesResponseType(typeof(SprintOptimizerJobDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<SprintOptimizerJobDto>> OptimizeSprint(
        [FromBody] OptimizeSprintCommand command)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var result = await _mediator.Send(command);
            return Ok(result.Payload);
        }
        catch (FeatureDisabledException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Title = "AI sprint optimizer is unavailable",
                Detail = ex.Message,
                Status = StatusCodes.Status503ServiceUnavailable
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Cannot start sprint optimizer", Detail = ex.Message });
        }
        catch (ForbiddenAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting sprint optimizer agent");
            return Fistix.TaskManager.ApiHost.Shared.Extensions.ApiErrorResponses.UnexpectedError(HttpContext, "Failed to start sprint optimizer");
        }
    }
    [HttpGet("agent/sprint-optimizer/active")]
    [ProducesResponseType(typeof(SprintOptimizerJobDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<SprintOptimizerJobDto>> GetActiveSprintOptimizer()
    {
        try
        {
            var result = await _mediator.Send(new GetActiveSprintOptimizerJobQuery());
            if (result.Payload is null)
            {
                return NoContent();
            }

            return Ok(result.Payload);
        }
        catch (ForbiddenAccessException)
        {
            return Forbid();
        }
    }
    [HttpGet("agent/sprint-optimizer/{jobExternalId:guid}")]
    [ProducesResponseType(typeof(SprintOptimizerJobDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SprintOptimizerJobDto>> GetSprintOptimizer(Guid jobExternalId)
    {
        try
        {
            var result = await _mediator.Send(new GetSprintOptimizerJobQuery { JobExternalId = jobExternalId });
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
    }
    [HttpPost("agent/sprint-optimizer/{jobExternalId:guid}/cancel")]
    [ProducesResponseType(typeof(SprintOptimizerJobDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SprintOptimizerJobDto>> CancelSprintOptimizer(Guid jobExternalId)
    {
        try
        {
            var result = await _mediator.Send(new CancelSprintOptimizerJobCommand { JobExternalId = jobExternalId });
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
    }
    [HttpPost("agent/sprint-optimizer/{jobExternalId:guid}/approve")]
    [EnableRateLimiting(RateLimitPolicies.AiAgents)]
    [ProducesResponseType(typeof(SprintOptimizerJobDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SprintOptimizerJobDto>> ApproveSprintOptimizerProposal(
        Guid jobExternalId,
        [FromBody] ApproveSprintOptimizerProposalCommand command)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        command.JobExternalId = jobExternalId;

        try
        {
            var result = await _mediator.Send(command);
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
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Cannot approve sprint proposal", Detail = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving sprint optimizer proposal {JobId}", jobExternalId);
            return Fistix.TaskManager.ApiHost.Shared.Extensions.ApiErrorResponses.UnexpectedError(HttpContext, "Failed to approve sprint proposal");
        }
    }
    [HttpPost("agent/sprint-optimizer/{jobExternalId:guid}/reject")]
    [EnableRateLimiting(RateLimitPolicies.AiAgents)]
    [ProducesResponseType(typeof(SprintOptimizerJobDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SprintOptimizerJobDto>> RejectSprintOptimizerProposal(Guid jobExternalId)
    {
        try
        {
            var result = await _mediator.Send(new RejectSprintOptimizerProposalCommand { JobExternalId = jobExternalId });
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
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Cannot reject sprint proposal", Detail = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting sprint optimizer proposal {JobId}", jobExternalId);
            return Fistix.TaskManager.ApiHost.Shared.Extensions.ApiErrorResponses.UnexpectedError(HttpContext, "Failed to reject sprint proposal");
        }
    }
    /// <summary>
    /// Starts a durable AI batch job (embedding → classify → summarize) with pause/continue/cancel.
    /// </summary>
    [HttpPost("batch")]
    [ProducesResponseType(typeof(AiBatchJobDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AiBatchJobDto>> StartBatch([FromBody] StartAiBatchJobCommand command)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var result = await _mediator.Send(command);
            return Ok(result.Payload);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Cannot start batch job", Detail = ex.Message });
        }
        catch (ForbiddenAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting AI batch job");
            return Fistix.TaskManager.ApiHost.Shared.Extensions.ApiErrorResponses.UnexpectedError(HttpContext, "Failed to start AI batch job");
        }
    }
    [HttpGet("batch/active")]
    [ProducesResponseType(typeof(AiBatchJobDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<AiBatchJobDto>> GetActiveBatch()
    {
        try
        {
            var result = await _mediator.Send(new GetActiveAiBatchJobQuery());
            if (result.Payload is null)
            {
                return NoContent();
            }

            return Ok(result.Payload);
        }
        catch (ForbiddenAccessException)
        {
            return Forbid();
        }
    }
    [HttpGet("batch/{jobExternalId:guid}")]
    [ProducesResponseType(typeof(AiBatchJobDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AiBatchJobDto>> GetBatch(Guid jobExternalId)
    {
        try
        {
            var result = await _mediator.Send(new GetAiBatchJobQuery { JobExternalId = jobExternalId });
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
    }
    [HttpPost("batch/{jobExternalId:guid}/pause")]
    [ProducesResponseType(typeof(AiBatchJobDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AiBatchJobDto>> PauseBatch(Guid jobExternalId)
    {
        try
        {
            var result = await _mediator.Send(new PauseAiBatchJobCommand { JobExternalId = jobExternalId });
            return Ok(result.Payload);
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (ForbiddenAccessException)
        {
            return Forbid();
        }
    }
    [HttpPost("batch/{jobExternalId:guid}/continue")]
    [ProducesResponseType(typeof(AiBatchJobDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AiBatchJobDto>> ContinueBatch(Guid jobExternalId)
    {
        try
        {
            var result = await _mediator.Send(new ContinueAiBatchJobCommand { JobExternalId = jobExternalId });
            return Ok(result.Payload);
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (ForbiddenAccessException)
        {
            return Forbid();
        }
    }
    [HttpPost("batch/{jobExternalId:guid}/cancel")]
    [ProducesResponseType(typeof(AiBatchJobDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AiBatchJobDto>> CancelBatch(Guid jobExternalId)
    {
        try
        {
            var result = await _mediator.Send(new CancelAiBatchJobCommand { JobExternalId = jobExternalId });
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
    }
}
