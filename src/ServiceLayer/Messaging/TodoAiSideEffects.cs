#nullable enable

using Fistix.TaskManager.Contracts.Todos;

namespace Fistix.TaskManager.ServiceLayer.Messaging;

/// <summary>Shared rules for classify/embed side effects from todo bus messages (D29).</summary>
public static class TodoAiSideEffects
{
    public static bool ShouldRunFromCreated() => true;

    public static bool ShouldRunFromUpdated(TodoUpdated message) => message.TextChanged;
}
