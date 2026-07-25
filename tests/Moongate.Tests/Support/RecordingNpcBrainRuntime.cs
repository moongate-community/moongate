using Moongate.Core.Primitives;
using Moongate.Server.Abstractions.Data.AI;
using Moongate.Server.Abstractions.Interfaces.AI;
using Moongate.Server.Abstractions.Types;

namespace Moongate.Tests.Support;

/// <summary>Configurable in-memory NPC runtime that records bindings, invocations, resets and removals.</summary>
public sealed class RecordingNpcBrainRuntime : INpcBrainRuntime
{
    public Dictionary<string, BrainDescriptor> Descriptors { get; } = new(StringComparer.Ordinal);

    public Dictionary<Serial, string> Bindings { get; } = [];

    public HashSet<string> BrokenBrainIds { get; } = new(StringComparer.Ordinal);

    public Queue<NpcBrainInvocationResult> Results { get; } = [];

    public List<(Serial MobileId, string BrainId)> BindCalls { get; } = [];

    public List<(Serial MobileId, NpcBrainHookType Hook, BrainContext Context, NpcBrainEvent? Event)> Invocations { get; } = [];

    public List<Serial> ResetCalls { get; } = [];

    public List<Serial> UnbindCalls { get; } = [];

    public Func<Serial, NpcBrainHookType, BrainContext, NpcBrainEvent?, NpcBrainInvocationResult>?
        InvocationHandler
    { get; set; }

    public bool TryBind(Serial mobileId, string brainId, out BrainDescriptor? descriptor, out string? error)
    {
        BindCalls.Add((mobileId, brainId));

        if (BrokenBrainIds.Contains(brainId) || !Descriptors.TryGetValue(brainId, out descriptor))
        {
            descriptor = null;
            error = $"Brain '{brainId}' is unavailable.";
            return false;
        }

        Bindings[mobileId] = brainId;
        error = null;

        return true;
    }

    public bool TryGetDescriptor(Serial mobileId, out BrainDescriptor? descriptor)
    {
        descriptor = null;

        return Bindings.TryGetValue(mobileId, out var brainId) &&
               Descriptors.TryGetValue(brainId, out descriptor);
    }

    public NpcBrainInvocationResult Invoke(
        Serial mobileId,
        NpcBrainHookType hook,
        BrainContext context,
        NpcBrainEvent? brainEvent = null
    )
    {
        Invocations.Add((mobileId, hook, context, brainEvent));

        if (InvocationHandler is not null)
        {
            return InvocationHandler(mobileId, hook, context, brainEvent);
        }

        return Results.TryDequeue(out var result)
            ? result
            : NpcBrainInvocationResult.Succeeded(BrainDecision.Empty);
    }

    public bool TryReload(string brainId, out BrainDescriptor? descriptor, out string? error)
    {
        if (Descriptors.TryGetValue(brainId, out descriptor))
        {
            error = null;
            return true;
        }

        error = $"Brain '{brainId}' is unavailable.";
        return false;
    }

    public void Reset(Serial mobileId)
    {
        ResetCalls.Add(mobileId);
    }

    public void Unbind(Serial mobileId)
    {
        UnbindCalls.Add(mobileId);
        Bindings.Remove(mobileId);
    }
}
