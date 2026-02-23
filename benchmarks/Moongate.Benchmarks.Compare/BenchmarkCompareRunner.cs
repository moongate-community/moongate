using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Moongate.Network.Compression;
using Moongate.Network.Packets.Data.Packets;
using Moongate.Network.Packets.Incoming.Login;
using Moongate.Network.Packets.Incoming.System;
using Moongate.Network.Packets.Outgoing.Login;
using Moongate.Network.Packets.Registry;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Network.Spans;
using Moongate.UO.Data.Middlewares;
using Moongate.UO.Data.Packets.Data;

namespace Moongate.Benchmarks.Compare;

public sealed class BenchmarkCompareRunner
{
    private readonly PacketRegistry _packetRegistry = new();
    private readonly byte[] _loginSeedBuffer = new byte[21];
    private readonly ServerListPacket _serverListPacket = new();
    private readonly CompressionMiddleware _compressionMiddleware = new();
    private readonly byte[] _payload256 = new byte[256];
    private readonly byte[] _payload1024 = new byte[1024];
    private readonly byte[] _compressedBuffer = new byte[NetworkCompression.BufferSize];
    private readonly byte[] _decompressedBuffer = new byte[NetworkCompression.BufferSize];
    private readonly List<byte[]> _streamChunks = [];
    private readonly List<byte> _streamPendingBytes = [];

    public BenchmarkCompareRunner()
    {
        PacketTable.Register(_packetRegistry);

        _loginSeedBuffer[0] = 0xEF;
        _loginSeedBuffer[8] = 0x07;
        _loginSeedBuffer[20] = 0x72;

        _serverListPacket.Shards.Clear();
        _serverListPacket.Shards.Add(
            new GameServerEntry
            {
                Index = 0,
                ServerName = "Moongate",
                IpAddress = IPAddress.Loopback
            }
        );
        _serverListPacket.Shards.Add(
            new GameServerEntry
            {
                Index = 1,
                ServerName = "Moongate Test",
                IpAddress = IPAddress.Parse("192.168.1.11")
            }
        );

        var random = new Random(1337);
        random.NextBytes(_payload256);
        random.NextBytes(_payload1024);

        BuildStreamChunks();
    }

    public IReadOnlyList<BenchmarkRunResult> Run(int iterations)
    {
        var results = new List<BenchmarkRunResult>(6)
        {
            Measure("ParseLoginSeedPacket", iterations, ParseLoginSeedPacket),
            Measure("WriteServerListPacket", iterations, WriteServerListPacket),
            Measure("ParseMixedPacketStreamInChunks", Math.Max(10_000, iterations / 20), ParseMixedPacketStreamInChunks),
            Measure("Compress256Bytes", iterations, Compress256Bytes),
            Measure(
                "CompressAndDecompress1024Bytes",
                Math.Max(20_000, iterations / 5),
                CompressAndDecompress1024Bytes
            ),
            Measure("CompressionMiddlewareProcessSend1024Bytes", iterations, CompressionMiddlewareProcessSend1024Bytes)
        };

        return results;
    }

    public static void WriteResults(IReadOnlyList<BenchmarkRunResult> results, CompareOptions options)
    {
        if (options.JsonOutput)
        {
            var json = JsonSerializer.Serialize(
                results,
                MoongateBenchmarksCompareJsonContext.Default.ListBenchmarkRunResult
            );

            if (!string.IsNullOrWhiteSpace(options.OutputPath))
            {
                File.WriteAllText(options.OutputPath, json);
            }
            else
            {
                Console.WriteLine(json);
            }

            return;
        }

        Console.WriteLine($"Iterations: {options.Iterations}");
        foreach (var result in results)
        {
            Console.WriteLine(
                $"{result.Name}: {result.MeanNanoseconds:F2} ns/op, {result.AllocatedBytesPerOperation:F2} B/op"
            );
        }
    }

    private BenchmarkRunResult Measure(string name, int iterations, Action action)
    {
        for (var i = 0; i < Math.Max(1, iterations / 10); i++)
        {
            action();
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var startAllocated = GC.GetAllocatedBytesForCurrentThread();
        var start = Stopwatch.GetTimestamp();

        for (var i = 0; i < iterations; i++)
        {
            action();
        }

        var elapsed = Stopwatch.GetTimestamp() - start;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - startAllocated;
        var nanoseconds = elapsed * 1_000_000_000.0 / Stopwatch.Frequency;

        return new BenchmarkRunResult
        {
            Name = name,
            MeanNanoseconds = nanoseconds / iterations,
            AllocatedBytesPerOperation = allocated / (double)iterations
        };
    }

    private void ParseLoginSeedPacket()
    {
        if (_packetRegistry.TryCreatePacket(0xEF, out var packet) && packet is LoginSeedPacket)
        {
            BenchmarkSink.Value = packet.TryParse(_loginSeedBuffer) ? 1 : 0;
        }
    }

    private void WriteServerListPacket()
    {
        var writer = new SpanWriter(128, true);

        try
        {
            _serverListPacket.Write(ref writer);
            BenchmarkSink.Value = writer.BytesWritten;
        }
        finally
        {
            writer.Dispose();
        }
    }

    private void ParseMixedPacketStreamInChunks()
    {
        _streamPendingBytes.Clear();
        var parsed = 0;

        for (var i = 0; i < _streamChunks.Count; i++)
        {
            _streamPendingBytes.AddRange(_streamChunks[i]);
            parsed += ParseAvailablePackets(_streamPendingBytes);
        }

        BenchmarkSink.Value = parsed;
    }

    private int ParseAvailablePackets(List<byte> pendingBytes)
    {
        var parsed = 0;

        while (pendingBytes.Count > 0)
        {
            var opCode = pendingBytes[0];

            if (!_packetRegistry.TryGetDescriptor(opCode, out var descriptor))
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

            if (_packetRegistry.TryCreatePacket(opCode, out var packet) && packet is not null && packet.TryParse(rawPacket))
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

    private void Compress256Bytes()
    {
        BenchmarkSink.Value = NetworkCompression.Compress(_payload256, _compressedBuffer);
    }

    private void CompressAndDecompress1024Bytes()
    {
        var compressedLength = NetworkCompression.Compress(_payload1024, _compressedBuffer);
        if (compressedLength <= 0)
        {
            BenchmarkSink.Value = 0;
            return;
        }

        BenchmarkSink.Value = NetworkCompression.Decompress(
            _compressedBuffer.AsSpan(0, compressedLength),
            _decompressedBuffer
        );
    }

    private void CompressionMiddlewareProcessSend1024Bytes()
    {
        var result = _compressionMiddleware.ProcessSendAsync(null, _payload1024).AsTask().GetAwaiter().GetResult();
        BenchmarkSink.Value = result.Length;
    }

    private void BuildStreamChunks()
    {
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
            _streamChunks.Add(stream.GetRange(offset, length).ToArray());
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
}
