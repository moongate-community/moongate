using Moongate.Core.Network.Compression;
using Moongate.Core.Network.Middleware;

namespace Moongate.UO.Data.Middlewares;

public class CompressionMiddleware : INetMiddleware
{
    public (bool halt, int consumedFromOrigin) ProcessReceive(
        ref ReadOnlyMemory<byte> input,
        out ReadOnlyMemory<byte> output
    )
    {
        var result = NetworkCompression.ProcessReceive(ref input, out var ot);

        output = ot;

        return (result.halt, result.consumedFromOrigin);
    }

    public void ProcessSend(ref ReadOnlyMemory<byte> input, out ReadOnlyMemory<byte> output)
    {
        var outputSpan = new Span<byte>(new byte[NetworkCompression.CalculateMaxCompressedSize(input.Length)]);
        var compressionLength = NetworkCompression.Compress(input.Span, outputSpan);
        output = outputSpan[..compressionLength].ToArray();
    }
}
