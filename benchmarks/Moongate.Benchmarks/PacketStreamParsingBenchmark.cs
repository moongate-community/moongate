using System.Buffers.Binary;
using BenchmarkDotNet.Attributes;
using Moongate.Network.Packets.Data.Packets;
using Moongate.Network.Packets.Incoming.System;
using Moongate.Network.Packets.Registry;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Network.Spans;

namespace Moongate.Benchmarks;

[MemoryDiagnoser]
public class PacketStreamParsingBenchmark
{
    private readonly PacketRegistry _registry = new();
    private readonly List<byte[]> _chunks = [];
    private readonly List<byte> _pendingBytes = [];

    [Benchmark]
    public int ParseMixedPacketStreamInChunks()
    {
        _pendingBytes.Clear();
        var parsed = 0;

        for (var i = 0; i < _chunks.Count; i++)
        {
            _pendingBytes.AddRange(_chunks[i]);
            parsed += ParseAvailablePackets(_pendingBytes);
        }

        return parsed;
    }

    [GlobalSetup]
    public void Setup()
    {
        PacketTable.Register(_registry);

        var ping = new byte[] { 0x73, 0x3A };
        var move = new byte[] { 0x02, 0x81, 0x10, 0x00, 0x00, 0x00, 0x01 };
        var generalInfo = BuildGeneralInformationPacket();

        var stream = new List<byte>(32 * 1024);

        for (var i = 0; i < 256; i++)
        {
            stream.AddRange(ping);
            stream.AddRange(move);
            stream.AddRange(generalInfo);
        }

        const int chunkSize = 64;

        for (var offset = 0; offset < stream.Count; offset += chunkSize)
        {
            var length = Math.Min(chunkSize, stream.Count - offset);
            _chunks.Add(stream.GetRange(offset, length).ToArray());
        }
    }

    private static byte[] BuildGeneralInformationPacket()
    {
        var packet = GeneralInformationPacket.CreateSetCursorHueSetMap(0);
        var writer = new SpanWriter(32, true);

        try
        {
            packet.Write(ref writer);

            return writer.ToArray();
        }
        finally
        {
            writer.Dispose();
        }
    }

    private int ParseAvailablePackets(List<byte> pendingBytes)
    {
        var parsed = 0;

        while (pendingBytes.Count > 0)
        {
            var opCode = pendingBytes[0];

            if (!_registry.TryGetDescriptor(opCode, out var descriptor))
            {
                pendingBytes.RemoveAt(0);

                continue;
            }

            var expectedLength = ResolvePacketLength(pendingBytes, descriptor);

            if (expectedLength is null || expectedLength.Value <= 0 || pendingBytes.Count < expectedLength.Value)
            {
                break;
            }

            var rawPacket = new byte[expectedLength.Value];
            pendingBytes.CopyTo(0, rawPacket, 0, expectedLength.Value);
            pendingBytes.RemoveRange(0, expectedLength.Value);

            if (_registry.TryCreatePacket(opCode, out var packet) && packet is not null && packet.TryParse(rawPacket))
            {
                parsed++;
            }
        }

        return parsed;
    }

    private static int? ResolvePacketLength(List<byte> pendingBytes, PacketDescriptor descriptor)
    {
        if (descriptor.Sizing == PacketSizing.Fixed)
        {
            return descriptor.Length;
        }

        if (pendingBytes.Count < 3)
        {
            return null;
        }

        Span<byte> lengthBuffer = stackalloc byte[2];
        lengthBuffer[0] = pendingBytes[1];
        lengthBuffer[1] = pendingBytes[2];

        return BinaryPrimitives.ReadUInt16BigEndian(lengthBuffer);
    }
}
