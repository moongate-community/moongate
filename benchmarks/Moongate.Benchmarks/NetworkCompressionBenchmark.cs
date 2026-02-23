using BenchmarkDotNet.Attributes;
using Moongate.Network.Compression;
using Moongate.UO.Data.Middlewares;

namespace Moongate.Benchmarks;

[MemoryDiagnoser]
public class NetworkCompressionBenchmark
{
    private readonly CompressionMiddleware _compressionMiddleware = new();
    private readonly byte[] _payload256 = new byte[256];
    private readonly byte[] _payload1024 = new byte[1024];
    private readonly byte[] _compressedBuffer256 = new byte[NetworkCompression.BufferSize];
    private readonly byte[] _compressedBuffer1024 = new byte[NetworkCompression.BufferSize];
    private readonly byte[] _decompressedBuffer1024 = new byte[NetworkCompression.BufferSize];

    [Benchmark]
    public int Compress256Bytes()
        => NetworkCompression.Compress(_payload256, _compressedBuffer256);

    [Benchmark]
    public int CompressAndDecompress1024Bytes()
    {
        var compressedLength = NetworkCompression.Compress(_payload1024, _compressedBuffer1024);

        if (compressedLength == 0)
        {
            return 0;
        }

        return NetworkCompression.Decompress(
            _compressedBuffer1024.AsSpan(0, compressedLength),
            _decompressedBuffer1024
        );
    }

    [Benchmark]
    public async Task<int> CompressionMiddlewareProcessSend1024Bytes()
    {
        var result = await _compressionMiddleware.ProcessSendAsync(null, _payload1024);

        return result.Length;
    }

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(1337);
        random.NextBytes(_payload256);
        random.NextBytes(_payload1024);
    }
}
