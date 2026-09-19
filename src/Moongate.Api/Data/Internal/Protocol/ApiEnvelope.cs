using Moongate.Api.Types.Protocol;

namespace Moongate.Api.Data.Internal.Protocol;

internal sealed record ApiEnvelope
{
    public ApiMessageKind Kind { get; }
    public uint RequestId { get; }
    public ushort OperationId { get; }
    public ReadOnlyMemory<byte> Payload { get; }

    public ApiEnvelope(ApiMessageKind kind, uint requestId, ushort operationId, ReadOnlyMemory<byte> payload)
    {
        Kind = kind;
        RequestId = requestId;
        OperationId = operationId;
        Payload = payload;
    }
}
