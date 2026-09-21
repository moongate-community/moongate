using System.Buffers;
using System.Buffers.Binary;
using MessagePack;
using Moongate.Api.Data.Internal.Protocol;
using Moongate.Api.Exceptions;
using Moongate.Api.Framing;
using Moongate.Api.Types.Protocol;

namespace Moongate.Api.Serialization.Internal;

internal sealed class ApiFrameCodec
{
    private const byte ProtocolVersion = 1;
    private const int FieldCount = 5;
    private const int MaximumHeaderLength = 16;
    private readonly int _maxFrameLength;

    public ApiFrameCodec(int maxFrameLength)
    {
        _ = new ApiFrameFramer(maxFrameLength);
        _maxFrameLength = maxFrameLength;
    }

    public ApiEnvelope Decode(ReadOnlyMemory<byte> frame)
    {
        try
        {
            if (frame.Length < sizeof(uint) ||
                frame.Length - sizeof(uint) > _maxFrameLength ||
                BinaryPrimitives.ReadUInt32BigEndian(frame.Span) != frame.Length - sizeof(uint))
            {
                throw new ApiProtocolException("Invalid frame length.");
            }

            var reader = new MessagePackReader(frame[sizeof(uint)..]);

            if (reader.ReadArrayHeader() != FieldCount || reader.ReadByte() != ProtocolVersion)
            {
                throw new ApiProtocolException("Unsupported envelope or protocol version.");
            }

            var kind = (ApiMessageKind)reader.ReadByte();
            var requestId = reader.ReadUInt32();
            var operationId = reader.ReadUInt16();
            ValidateFields(kind, requestId, operationId);

            if (reader.NextMessagePackType != MessagePackType.Binary || reader.ReadBytes() is not { } payload || !reader.End)
            {
                throw new ApiProtocolException("Invalid envelope payload.");
            }

            return new(kind, requestId, operationId, payload.ToArray());
        }
        catch (Exception exception) when (exception is MessagePackSerializationException or
                                              EndOfStreamException or
                                              OverflowException)
        {
            throw new ApiProtocolException("Malformed API envelope.", exception);
        }
    }

    public byte[] Encode(ApiEnvelope envelope)
    {
        ValidateFields(envelope.Kind, envelope.RequestId, envelope.OperationId);

        if (envelope.Payload.Length > _maxFrameLength)
        {
            throw new ApiProtocolException("Frame limit exceeded.");
        }

        var buffer = new ArrayBufferWriter<byte>(
            Math.Max(
                MaximumHeaderLength,
                Math.Min(_maxFrameLength, envelope.Payload.Length + MaximumHeaderLength)
            )
        );
        var writer = new MessagePackWriter(buffer);
        writer.WriteArrayHeader(FieldCount);
        writer.Write(ProtocolVersion);
        writer.Write((byte)envelope.Kind);
        writer.Write(envelope.RequestId);
        writer.Write(envelope.OperationId);
        writer.WriteBinHeader(envelope.Payload.Length);
        writer.Flush();
        var length = checked(buffer.WrittenCount + envelope.Payload.Length);

        if (length > _maxFrameLength)
        {
            throw new ApiProtocolException("Frame limit exceeded.");
        }

        var frame = new byte[length + sizeof(uint)];
        BinaryPrimitives.WriteUInt32BigEndian(frame, (uint)length);
        buffer.WrittenSpan.CopyTo(frame.AsSpan(sizeof(uint)));
        envelope.Payload.Span.CopyTo(frame.AsSpan(sizeof(uint) + buffer.WrittenCount));

        return frame;
    }

    private static void ValidateFields(ApiMessageKind kind, uint requestId, ushort operationId)
    {
        if (kind is not (ApiMessageKind.Request or ApiMessageKind.Response or ApiMessageKind.Error) ||
            requestId == 0 ||
            operationId == 0)
        {
            throw new ApiProtocolException("Invalid envelope identifiers.");
        }
    }
}
