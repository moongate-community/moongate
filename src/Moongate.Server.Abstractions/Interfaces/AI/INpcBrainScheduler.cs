using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.AI;
using Moongate.Server.Abstractions.Types;

namespace Moongate.Server.Abstractions.Interfaces.AI;

/// <summary>Coordinates NPC brain lifecycles, queued events and scheduled execution.</summary>
public interface INpcBrainScheduler
{
    /// <summary>Gets the greatest hearing range of all scheduled brains.</summary>
    int MaxHearingRange { get; }

    /// <summary>Gets the greatest perception range of all scheduled brains.</summary>
    int MaxPerceptionRange { get; }

    /// <summary>Activates a mobile's scheduled brain.</summary>
    void Activate(Serial mobileId);

    /// <summary>Binds a mobile to its configured brain.</summary>
    void Bind(MobileEntity mobile);

    /// <summary>Begins deactivating a mobile's scheduled brain.</summary>
    void Deactivate(Serial mobileId);

    /// <summary>Queues a brain event for delivery on the mobile's next wake.</summary>
    void EnqueueEvent(Serial mobileId, NpcBrainHookType hook, NpcBrainEvent brainEvent);

    /// <summary>Returns whether a mobile's brain is active.</summary>
    bool IsActive(Serial mobileId);

    /// <summary>Refreshes every scheduled brain using the supplied descriptor.</summary>
    void RefreshDescriptor(string brainId, BrainDescriptor descriptor);

    /// <summary>Executes due brain work for the current scheduler iteration.</summary>
    void Tick();

    /// <summary>Attempts to obtain the descriptor scheduled for a mobile.</summary>
    bool TryGetDescriptor(Serial mobileId, out BrainDescriptor? descriptor);

    /// <summary>Removes a mobile and its brain from scheduling.</summary>
    void Unbind(Serial mobileId);
}
