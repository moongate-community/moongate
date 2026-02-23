using System.Net;
using BenchmarkDotNet.Attributes;
using Moongate.Network.Packets.Outgoing.Login;
using Moongate.Network.Spans;

namespace Moongate.Benchmarks;

[MemoryDiagnoser]
public class PacketSerializationBenchmark
{
    private readonly ServerListPacket _packet = new();

    [GlobalSetup]
    public void Setup()
    {
        _packet.Shards.Clear();
        _packet.Shards.Add(
            new()
            {
                Index = 0,
                ServerName = "Moongate",
                IpAddress = IPAddress.Loopback
            }
        );
        _packet.Shards.Add(
            new()
            {
                Index = 1,
                ServerName = "Moongate Test",
                IpAddress = IPAddress.Parse("192.168.1.11")
            }
        );
    }

    [Benchmark]
    public int WriteServerListPacket()
    {
        var writer = new SpanWriter(128, true);

        try
        {
            _packet.Write(ref writer);

            return writer.BytesWritten;
        }
        finally
        {
            writer.Dispose();
        }
    }
}
