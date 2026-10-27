#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Fistix.TaskManager.Core.Abstractions.Repositories;
using Fistix.TaskManager.Core.DomainModel.Aggregates;
using Fistix.TaskManager.Core.DomainModel.Constants;
using Fistix.TaskManager.ServiceLayer.Notifications;
using Fistix.TaskManager.ServiceLayer.Todos;
using Fistix.TaskManager.ViewModel.Dtos;
using Xunit;

namespace Fistix.TaskManager.ServiceLayer.Tests;

public class SprintApproveFinalizeServiceTests
{
    [Fact]
    public async Task CompleteAsync_SetsJobCompleted_AndNotifies()
    {
        var job = new SprintOptimizerJob
        {
            Status = AiBatchJobStatus.Running,
            CurrentPhase = SprintOptimizerPhase.Persisting,
            MaxTasks = 5,
            DurationDays = 14,
            CreatedByUserId = Guid.NewGuid()
        };
        job.GenerateNewExternalId();

        var repo = new FakeJobRepository(job);
        var notifier = new FakeNotifier();
        var service = new SprintApproveFinalizeService(repo, notifier);

        var sprintId = Guid.NewGuid();
        var tasksJson = JsonSerializer.Serialize(new List<SprintTaskSummaryDto>
        {
            new() { ExternalId = Guid.NewGuid(), Title = "A", Priority = "High", Status = "Pending" }
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        await service.CompleteAsync(
            job.ExternalId,
            sprintId,
            "Sprint A",
            DateTime.UtcNow.Date,
            DateTime.UtcNow.Date.AddDays(14),
            tasksJson,
            "because",
            "[]",
            CancellationToken.None);

        Assert.Equal(AiBatchJobStatus.Completed, job.Status);
        Assert.Equal(SprintOptimizerPhase.Done, job.CurrentPhase);
        Assert.Equal(sprintId, job.CreatedSprintId);
        Assert.Equal(1, notifier.Count);
    }

    [Fact]
    public async Task FailAsync_SetsJobFailed_AndNotifies()
    {
        var job = new SprintOptimizerJob
        {
            Status = AiBatchJobStatus.Running,
            CurrentPhase = SprintOptimizerPhase.Persisting,
            CreatedByUserId = Guid.NewGuid()
        };
        job.GenerateNewExternalId();

        var repo = new FakeJobRepository(job);
        var notifier = new FakeNotifier();
        var service = new SprintApproveFinalizeService(repo, notifier);

        await service.FailAsync(job.ExternalId, "boom", CancellationToken.None);

        Assert.Equal(AiBatchJobStatus.Failed, job.Status);
        Assert.Equal("boom", job.LastError);
        Assert.Equal(1, notifier.Count);
    }

    private sealed class FakeJobRepository(SprintOptimizerJob job) : ISprintOptimizerJobRepository
    {
        public Task CreateAsync(SprintOptimizerJob j, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task UpdateAsync(SprintOptimizerJob j, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<SprintOptimizerJob?> GetByExternalIdAsync(Guid externalId, CancellationToken cancellationToken)
            => Task.FromResult<SprintOptimizerJob?>(job.ExternalId == externalId ? job : null);
        public Task<SprintOptimizerJob?> GetActiveByOwnerAsync(Guid ownerExternalId, CancellationToken cancellationToken)
            => Task.FromResult<SprintOptimizerJob?>(null);
        public Task<IReadOnlyList<SprintOptimizerJob>> GetRunnableAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<SprintOptimizerJob>>([]);
        public Task<IReadOnlyList<SprintOptimizerJob>> GetStaleRunningAsync(TimeSpan staleAfter, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<SprintOptimizerJob>>([]);
    }

    private sealed class FakeNotifier : ISprintOptimizerNotifier
    {
        public int Count { get; private set; }
        public Task NotifyAsync(SprintOptimizerJobDto job, CancellationToken cancellationToken = default)
        {
            Count++;
            return Task.CompletedTask;
        }
    }
}
