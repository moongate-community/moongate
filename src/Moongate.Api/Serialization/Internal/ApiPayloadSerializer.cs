using System.Text;
using MessagePack;
using Moongate.Api.Buffers.Internal;
using Moongate.Api.Exceptions;

namespace Moongate.Api.Serialization.Internal;

internal static class ApiPayloadSerializer
{
    private const int MaxDepth = 64;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    private static readonly MessagePackSerializerOptions Options = MessagePackSerializerOptions.Standard
        .WithSecurity(MessagePackSecurity.UntrustedData.WithMaximumObjectGraphDepth(MaxDepth));

    public static T Deserialize<T>(ReadOnlyMemory<byte> payload)
    {
        Validate(payload);
        var reader = new MessagePackReader(payload);
        var result = MessagePackSerializer.Deserialize<T>(ref reader, Options);

        if (result is null || !reader.End)
        {
            throw new MessagePackSerializationException("Invalid typed payload.");
        }

        return result;
    }

    public static byte[] Serialize<T>(T value, int maxLength)
    {
        var buffer = new BoundedBufferWriter(maxLength);

        try
        {
            MessagePackSerializer.Serialize(buffer, value, Options);
        }
        catch (MessagePackSerializationException exception) when (exception.GetBaseException() is ApiProtocolException limit)
        {
            throw new ApiProtocolException("Payload limit exceeded.", limit);
        }

        Validate(buffer.WrittenMemory);

        return buffer.WrittenMemory.ToArray();
    }

    private static void Scan(ref MessagePackReader reader, int length, int depth)
    {
        if (depth > MaxDepth)
        {
            throw new ApiProtocolException("Payload nesting limit exceeded.");
        }

        switch (reader.NextMessagePackType)
        {
            case MessagePackType.Array:
                var count = reader.ReadArrayHeader();

                if (count > length - reader.Consumed)
                {
                    throw new ApiProtocolException("Invalid payload array length.");
                }

                for (var i = 0; i < count; i++)
                {
                    Scan(ref reader, length, depth + 1);
                }

                break;
            case MessagePackType.Binary:
                reader.ReadBytes();

                break;
            case MessagePackType.String:
                var text = reader.ReadStringSequence();

                if (text is { } bytes)
                {
                    StrictUtf8.GetCharCount(bytes.FirstSpan);
                }

                break;
            case MessagePackType.Integer:
            case MessagePackType.Boolean:
            case MessagePackType.Nil:
                reader.Skip();

                break;
            default:
                throw new MessagePackSerializationException("Unsupported payload encoding.");
        }
    }

    private static void Validate(ReadOnlyMemory<byte> payload)
    {
        try
        {
            var reader = new MessagePackReader(payload);
            Scan(ref reader, payload.Length, 0);

            if (!reader.End)
            {
                throw new MessagePackSerializationException("Trailing payload data.");
            }
        }
        catch (Exception exception) when (exception is EndOfStreamException or OverflowException)
        {
            throw new ApiProtocolException("Payload length exceeds available data.", exception);
        }
        catch (DecoderFallbackException exception)
        {
            throw new MessagePackSerializationException("Invalid UTF-8 payload.", exception);
        }
    }
}
