using BenchmarkDotNet.Attributes;
using Moongate.Network.Packets.Incoming.Login;
using Moongate.Server.Data.Packets;
using Moongate.Server.Services.Messaging;
using Moongate.Server.Services.Packets;

namespace Moongate.Benchmarks;

[MemoryDiagnoser]
public class QueueThroughputBenchmark
{
    private const int Operations = 1024;

    private readonly MessageBusService _messageBusService = new();
    private readonly OutgoingPacketQueue _outgoingPacketQueue = new();
    private readonly LoginSeedPacket _packet = new();

    [Benchmark]
    public int MessageBusPublishThenDrain()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        for (var i = 0; i < Operations; i++)
        {
            var packet = new IncomingGamePacket(null!, 0xEF, _packet, timestamp);
            _messageBusService.PublishIncomingPacket(packet);
        }

        var drained = 0;

        while (_messageBusService.TryReadIncomingPacket(out _))
        {
            drained++;
        }

        return drained;
    }

    [Benchmark]
    public int OutgoingQueueEnqueueThenDrain()
    {
        for (var i = 0; i < Operations; i++)
        {
            _outgoingPacketQueue.Enqueue(1, _packet);
        }

        var drained = 0;

        while (_outgoingPacketQueue.TryDequeue(out _))
        {
            drained++;
        }

        return drained;
    }
}
