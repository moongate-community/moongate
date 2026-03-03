using BenchmarkDotNet.Attributes;
using Moongate.Network.Packets.Incoming.Interaction;
using Moongate.Network.Packets.Incoming.Movement;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Network.Packets.Registry;
using Moongate.Network.Spans;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Benchmarks;

[MemoryDiagnoser]
public class PacketGameplayHotPathBenchmark
{
    private readonly PacketRegistry _registry = new();

    private readonly byte[] _moveRequestPacketBuffer = [0x02, 0x81, 0x24, 0x00, 0x00, 0x00, 0x01];
    private readonly byte[] _pickUpItemPacketBuffer = [0x07, 0x40, 0x00, 0x00, 0x42, 0x01, 0xF4];
    private readonly byte[] _dropItemPacketBuffer =
    [
        0x08,
        0x40,
        0x00,
        0x00,
        0x42,
        0x0D,
        0x88,
        0x0A,
        0x20,
        0x00,
        0x40,
        0x00,
        0x00,
        0x10
    ];

    private readonly byte[] _dropWearItemPacketBuffer = [0x13, 0x40, 0x00, 0x00, 0x42, 0x14, 0x00, 0x00, 0x00, 0x02];

    private ObjectInformationPacket _objectInformationPacket = null!;
    private DraggingOfItemPacket _draggingOfItemPacket = null!;

    [Benchmark]
    public bool ParseMoveRequestPacket()
        => ParsePacket(0x02, _moveRequestPacketBuffer);

    [Benchmark]
    public bool ParsePickUpItemPacket()
        => ParsePacket(0x07, _pickUpItemPacketBuffer);

    [Benchmark]
    public bool ParseDropItemPacket()
        => ParsePacket(0x08, _dropItemPacketBuffer);

    [Benchmark]
    public bool ParseDropWearItemPacket()
        => ParsePacket(0x13, _dropWearItemPacketBuffer);

    [Benchmark(OperationsPerInvoke = 200)]
    public int ParseMixedGameplayPacketBurst()
    {
        var parsed = 0;

        for (var i = 0; i < 50; i++)
        {
            parsed += ParsePacket(0x02, _moveRequestPacketBuffer) ? 1 : 0;
            parsed += ParsePacket(0x07, _pickUpItemPacketBuffer) ? 1 : 0;
            parsed += ParsePacket(0x08, _dropItemPacketBuffer) ? 1 : 0;
            parsed += ParsePacket(0x13, _dropWearItemPacketBuffer) ? 1 : 0;
        }

        return parsed;
    }

    [Benchmark]
    public int WriteObjectInformationPacket()
    {
        var writer = new SpanWriter(64, true);

        try
        {
            _objectInformationPacket.Write(ref writer);

            return writer.BytesWritten;
        }
        finally
        {
            writer.Dispose();
        }
    }

    [Benchmark]
    public int WriteDraggingOfItemPacket()
    {
        var writer = new SpanWriter(64, true);

        try
        {
            _draggingOfItemPacket.Write(ref writer);

            return writer.BytesWritten;
        }
        finally
        {
            writer.Dispose();
        }
    }

    [GlobalSetup]
    public void Setup()
    {
        PacketTable.Register(_registry);

        var item = new UOItemEntity
        {
            Id = (Serial)0x4000_0042,
            ItemId = 0x0EED,
            Amount = 500,
            Hue = 0,
            Direction = DirectionType.North,
            Location = new Point3D(3472, 2592, 0)
        };

        _objectInformationPacket = new(item);
        _draggingOfItemPacket = new(
            itemId: 0x0EED,
            hue: 0,
            stackCount: 500,
            sourceId: (Serial)0x4000_0010,
            sourceLocation: new Point3D(90, 110, 0),
            targetId: (Serial)0x0000_0002,
            targetLocation: new Point3D(3472, 2592, 0)
        );
    }

    private bool ParsePacket(byte opCode, byte[] payload)
    {
        if (!_registry.TryCreatePacket(opCode, out var packet) || packet is null)
        {
            return false;
        }

        return packet.TryParse(payload);
    }
}
