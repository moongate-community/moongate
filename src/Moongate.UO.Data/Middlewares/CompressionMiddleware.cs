using Moongate.Core.Network.Compression;
using Moongate.Core.Network.Middleware;

namespace Moongate.UO.Data.Middlewares;

public class CompressionMiddleware : INetMiddleware
{
    public void ProcessSend(ref ReadOnlyMemory<byte> input, out ReadOnlyMemory<byte> output)
    {
        NetworkCompression.ProcessSend(ref input, out ReadOnlyMemory<byte> ot);
        output = ot;
    }

    public (bool halt, int consumedFromOrigin) ProcessReceive(ref ReadOnlyMemory<byte> input, out ReadOnlyMemory<byte> output)
    {
        var result =  NetworkCompression.ProcessReceive(ref input, out ReadOnlyMemory<byte> ot);

        output = ot;

        return (result.halt, result.consumedFromOrigin);

    }
}
