using Moongate.Server.Data.Session;
using SquidStd.Core.Interfaces.Events;

namespace Moongate.Server.Data.Events;

/// <summary>Raised after an incoming packet is routed to its registered handler.</summary>
public sealed record PacketDispatchedEvent(PlayerSession Session, byte OpCode) : IEvent;
