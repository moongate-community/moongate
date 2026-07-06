namespace Moongate.Network.Handlers;

/// <summary>
/// Maps packet ids to handlers. Registration happens once at startup (bootstrap);
/// dispatch runs on network threads and is read-only, so no locking is needed.
/// </summary>
public sealed class PacketHandlerRegistry
{
    private readonly PacketHandlerDelegate?[] _handlers = new PacketHandlerDelegate?[256];

    public void Register(byte packetId, PacketHandlerDelegate handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        if (_handlers[packetId] is not null)
        {
            throw new InvalidOperationException($"A handler for packet 0x{packetId:X2} is already registered.");
        }

        _handlers[packetId] = handler;
    }

    public bool TryDispatch(ReadOnlySpan<byte> packet)
    {
        if (packet.IsEmpty)
        {
            return false;
        }

        var handler = _handlers[packet[0]];

        if (handler is null)
        {
            return false;
        }

        handler(packet);

        return true;
    }
}
