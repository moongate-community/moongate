namespace Moongate.Api.Data.Internal.Requests;

internal sealed record ApiOutboundFrame
{
    public ReadOnlyMemory<byte> Bytes { get; }
    public uint? LocalRequestId { get; }

    public ApiOutboundFrame(byte[] bytes, uint? localRequestId)
    {
        Bytes = bytes;
        LocalRequestId = localRequestId;
    }
}
