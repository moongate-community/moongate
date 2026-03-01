using Moongate.Server.Data.Events.Base;

namespace Moongate.Server.Data.Events.Party;

/// <summary>
/// Event emitted for a client party-system command (0xBF/0x06).
/// </summary>
public readonly record struct PartySystemCommandEvent(
    GameEventBase BaseEvent,
    long SessionId,
    byte Subcommand,
    byte[] Payload
) : IGameEvent
{
    /// <summary>
    /// Creates a party-system command event with current timestamp.
    /// </summary>
    public PartySystemCommandEvent(long sessionId, byte subcommand, byte[] payload)
        : this(GameEventBase.CreateNow(), sessionId, subcommand, payload) { }
}
