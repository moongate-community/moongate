using Moongate.Core.Primitives;
using Moongate.Server.Abstractions.Data.AI;
using Moongate.Server.Abstractions.Interfaces.AI;

namespace Moongate.Tests.Support;

/// <summary>Records scheduler-to-intent-executor handoffs without applying world mutations.</summary>
public sealed class RecordingBrainIntentExecutor : IBrainIntentExecutor
{
    public List<(Serial MobileId, BrainContext Context, IReadOnlyList<BrainIntent> Intents)> Executions { get; } = [];

    public void Execute(Serial mobileId, BrainContext context, IReadOnlyList<BrainIntent> intents)
    {
        Executions.Add((mobileId, context, intents.ToArray()));
    }
}
