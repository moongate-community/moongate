using Moongate.Server.Abstractions.Data.Session;

namespace Moongate.Server.Services.Network;

/// <summary>
/// Non-generic entry point stored per opcode: reads the typed packet from the frame and
/// invokes the registered handler. Lets the generic handlers live behind an array lookup.
/// </summary>
internal delegate void PacketDispatch(PlayerSession session, ReadOnlySpan<byte> packet);
