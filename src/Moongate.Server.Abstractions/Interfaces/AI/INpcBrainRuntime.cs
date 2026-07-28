using Moongate.Core.Primitives;
using Moongate.Server.Abstractions.Data.AI;
using Moongate.Server.Abstractions.Types;

namespace Moongate.Server.Abstractions.Interfaces.AI;

/// <summary>Provides runtime binding and hook invocation for NPC brain implementations.</summary>
public interface INpcBrainRuntime
{
    /// <summary>Invokes a brain hook with the supplied world context and optional event.</summary>
    NpcBrainInvocationResult Invoke(
        Serial mobileId,
        NpcBrainHookType hook,
        BrainContext context,
        NpcBrainEvent? brainEvent = null
    );

    /// <summary>Resets the runtime state held for a mobile.</summary>
    void Reset(Serial mobileId);

    /// <summary>Attempts to bind a mobile to a brain and returns its descriptor on success.</summary>
    bool TryBind(Serial mobileId, string brainId, out BrainDescriptor? descriptor, out string? error);

    /// <summary>Attempts to obtain the descriptor bound to a mobile.</summary>
    bool TryGetDescriptor(Serial mobileId, out BrainDescriptor? descriptor);

    /// <summary>Attempts to reload a brain definition and returns its descriptor on success.</summary>
    bool TryReload(string brainId, out BrainDescriptor? descriptor, out string? error);

    /// <summary>Removes a mobile's brain binding and runtime state.</summary>
    void Unbind(Serial mobileId);
}
