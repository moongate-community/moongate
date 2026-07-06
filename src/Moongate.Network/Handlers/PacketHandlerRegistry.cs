using Moongate.Network.Protocol;
using Serilog;

namespace Moongate.Network.Handlers;

/// <summary>
/// Maps packet ids to handlers. Registration happens once at startup (bootstrap);
/// dispatch runs on network threads and is read-only, so no locking is needed.
/// A packet without a registered handler logs one warning per id (not per packet)
/// with its catalog name, so unimplemented packets surface without flooding the log.
/// </summary>
public sealed class PacketHandlerRegistry
{
    private static readonly ILogger _logger = Log.ForContext<PacketHandlerRegistry>();

    private readonly PacketHandlerDelegate?[] _handlers = new PacketHandlerDelegate?[256];
    private readonly bool[] _warned = new bool[256];

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

        var packetId = packet[0];
        var handler = _handlers[packetId];

        if (handler is null)
        {
            WarnUnhandled(packetId);

            return false;
        }

        handler(packet);

        return true;
    }

    private void WarnUnhandled(byte packetId)
    {
        if (_warned[packetId])
        {
            return;
        }

        _warned[packetId] = true;

        var info = PacketsInfo.GetPacket(packetId);

        _logger.Warning(
            "No handler registered for packet 0x{PacketId:X2} ({PacketName}) - packet not implemented yet",
            packetId,
            info?.Name ?? "Unknown"
        );
    }
}
