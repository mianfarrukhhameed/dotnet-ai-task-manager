#nullable enable

using Fistix.TaskManager.Contracts.Todos;
using Fistix.TaskManager.ServiceLayer.Messaging;
using Xunit;

namespace Fistix.TaskManager.ServiceLayer.Tests;

public class TodoUpdatedTextChangeGateTests
{
    [Fact]
    public void ShouldRunFromUpdated_False_WhenTextUnchanged()
    {
        var message = new TodoUpdated(Guid.NewGuid(), Guid.NewGuid(), TextChanged: false, DateTime.UtcNow);
        Assert.False(TodoAiSideEffects.ShouldRunFromUpdated(message));
    }

    [Fact]
    public void ShouldRunFromUpdated_True_WhenTextChanged()
    {
        var message = new TodoUpdated(Guid.NewGuid(), Guid.NewGuid(), TextChanged: true, DateTime.UtcNow);
        Assert.True(TodoAiSideEffects.ShouldRunFromUpdated(message));
    }

    [Fact]
    public void ShouldRunFromCreated_AlwaysTrue()
    {
        Assert.True(TodoAiSideEffects.ShouldRunFromCreated());
    }
}
