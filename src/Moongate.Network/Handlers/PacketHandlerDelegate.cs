namespace Moongate.Network.Handlers;

/// <summary>
/// Handles one complete framed packet (id at byte 0). Runs on the network thread:
/// decode here, post game mutations to the event loop dispatcher — never touch
/// game state directly.
/// </summary>
public delegate void PacketHandlerDelegate(ReadOnlySpan<byte> packet);
