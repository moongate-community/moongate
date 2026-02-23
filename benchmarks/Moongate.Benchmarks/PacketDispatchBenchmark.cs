using BenchmarkDotNet.Attributes;
using Moongate.Network.Packets.Incoming.Login;
using Moongate.Server.Data.Packets;
using Moongate.Server.Services.Packets;

namespace Moongate.Benchmarks;

[MemoryDiagnoser]
/// <summary>
/// Represents PacketDispatchBenchmark.
/// </summary>
public class PacketDispatchBenchmark
{
    private readonly PacketDispatchService _packetDispatchService = new();
    private readonly LoginSeedPacket _packet = new();
    private IncomingGamePacket _withListenersPacket;
    private IncomingGamePacket _withoutListenersPacket;

    [Benchmark]
    public bool DispatchToThreeListeners()
        => _packetDispatchService.NotifyPacketListeners(_withListenersPacket);

    [Benchmark]
    public bool DispatchWithoutListeners()
        => _packetDispatchService.NotifyPacketListeners(_withoutListenersPacket);

    [GlobalSetup]
    public void Setup()
    {
        _packetDispatchService.AddPacketListener(0xEF, new NoOpPacketListener());
        _packetDispatchService.AddPacketListener(0xEF, new NoOpPacketListener());
        _packetDispatchService.AddPacketListener(0xEF, new NoOpPacketListener());

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _withListenersPacket = new(null!, 0xEF, _packet, timestamp);
        _withoutListenersPacket = new(null!, 0x7E, _packet, timestamp);
    }
}
