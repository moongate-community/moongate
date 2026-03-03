using Moongate.Abstractions.Interfaces.Services.Base;
using Moongate.Server.Data.Events.Spatial;
using Moongate.Server.Data.Events.Speech;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Interfaces.Services.Scripting;

/// <summary>
/// Coordinates Lua brain execution and speech event delivery for NPCs.
/// </summary>
public interface ILuaBrainRunner : IMoongateService, IGameEventListener<SpeechHeardEvent>, IGameEventListener<MobileAddedInWorldEvent>
{
    /// <summary>
    /// Registers or updates a mobile brain runtime binding.
    /// </summary>
    /// <param name="mobile">Target mobile.</param>
    /// <param name="brainId">Brain identifier resolved by registry.</param>
    void Register(UOMobileEntity mobile, string brainId);

    /// <summary>
    /// Unregisters a mobile brain runtime binding.
    /// </summary>
    /// <param name="mobileId">Target mobile id.</param>
    void Unregister(Serial mobileId);

    /// <summary>
    /// Enqueues a speech event for brain processing.
    /// </summary>
    /// <param name="gameEvent">Speech event already filtered per target npc.</param>
    void EnqueueSpeech(SpeechHeardEvent gameEvent);

    /// <summary>
    /// Processes due brain ticks for all registered NPCs.
    /// </summary>
    /// <param name="nowMilliseconds">Current Unix timestamp in milliseconds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask TickAllAsync(long nowMilliseconds, CancellationToken cancellationToken = default);
}
