using System.Collections.Concurrent;
using System.Diagnostics;
using Moongate.Network.Packets.Registry;
using Moongate.Server.Data.Packets;
using Moongate.Server.Interfaces.Listener;
using Moongate.Server.Interfaces.Services.Packets;
using Serilog;

namespace Moongate.Server.Services.Packets;

/// <summary>
/// Represents PacketDispatchService.
/// </summary>
public class PacketDispatchService : IPacketDispatchService
{
    private const double SlowOpcodeDispatchThresholdMilliseconds = 100;
    private const double SlowListenerDispatchThresholdMilliseconds = 50;
    private static readonly PacketRegistry _packetRegistry = CreatePacketRegistry();
    private readonly ILogger _logger = Log.ForContext<PacketDispatchService>();
    private readonly ConcurrentDictionary<byte, List<IPacketListener>> _packetListeners = new();

    public void AddPacketListener(byte opCode, IPacketListener packetListener)
    {
        var listeners = _packetListeners.GetOrAdd(opCode, static _ => []);

        lock (listeners)
        {
            listeners.Add(packetListener);
        }

        _logger.Debug("Added packet listener for opcode 0x{OpCode:X2}", opCode);
    }

    public bool NotifyPacketListeners(IncomingGamePacket gamePacket)
    {
        var opCode = gamePacket.PacketId;

        if (!_packetListeners.TryGetValue(opCode, out var listeners))
        {
            if (_packetRegistry.TryGetDescriptor(opCode, out var descriptor))
            {
                _logger.Warning(
                    "No packet listeners for opcode 0x{OpCode:X2} ({Description})",
                    opCode,
                    descriptor.Description
                );
            }
            else
            {
                _logger.Warning("No packet listeners for opcode 0x{OpCode:X2}", opCode);
            }

            return false;
        }

        int listenerCount;

        lock (listeners)
        {
            listenerCount = listeners.Count;
        }

        if (listenerCount == 0)
        {
            return false;
        }

        var dispatchStart = Stopwatch.GetTimestamp();

        for (var i = 0; i < listenerCount; i++)
        {
            IPacketListener listener;

            lock (listeners)
            {
                if (i >= listeners.Count)
                {
                    break;
                }

                listener = listeners[i];
            }

            NotifyListenerSafe(opCode, gamePacket, listener);
        }

        var dispatchElapsed = Stopwatch.GetElapsedTime(dispatchStart);

        if (dispatchElapsed.TotalMilliseconds >= SlowOpcodeDispatchThresholdMilliseconds)
        {
            _logger.Warning(
                "Slow packet dispatch opcode=0x{OpCode:X2} listeners={ListenerCount} elapsed={ElapsedMs:0.###}ms session={SessionId} packet={PacketType}",
                opCode,
                listenerCount,
                dispatchElapsed.TotalMilliseconds,
                gamePacket.Session?.SessionId ?? 0,
                gamePacket.Packet.GetType().Name
            );
        }

        return true;
    }

    private static PacketRegistry CreatePacketRegistry()
    {
        var registry = new PacketRegistry();
        PacketTable.Register(registry);

        return registry;
    }

    private void NotifyListenerSafe(byte opCode, IncomingGamePacket gamePacket, IPacketListener listener)
    {
        var listenerStart = Stopwatch.GetTimestamp();

        try
        {
            var task = listener.HandlePacketAsync(gamePacket.Session, gamePacket.Packet);

            if (!task.IsCompletedSuccessfully)
            {
                task.GetAwaiter().GetResult();
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Listener failed for packet opcode 0x{OpCode:X2}", opCode);
        }
        finally
        {
            var elapsed = Stopwatch.GetElapsedTime(listenerStart);

            if (elapsed.TotalMilliseconds >= SlowListenerDispatchThresholdMilliseconds)
            {
                _logger.Warning(
                    "Slow packet listener opcode=0x{OpCode:X2} listener={ListenerType} elapsed={ElapsedMs:0.###}ms session={SessionId} packet={PacketType}",
                    opCode,
                    listener.GetType().FullName,
                    elapsed.TotalMilliseconds,
                    gamePacket.Session?.SessionId ?? 0,
                    gamePacket.Packet.GetType().Name
                );
            }
        }
    }
}
