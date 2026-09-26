using System.Buffers.Binary;
using Moongate.Network.Interfaces.Framing;

namespace Moongate.Network.Tests.Support;

/// <summary>
///     A stateful test framer with a 4 byte header carrying the payload length,
///     obfuscated with a running key that advances on every byte it transforms, followed by a payload
///     left in clear.
/// </summary>
/// <remarks>
///     The key advancing per byte is what makes this a regression test rather than a demonstration.
///     Decoding the same header twice advances the key twice and yields a different length, exactly the
///     way a real stream cipher desynchronises. A framer that re-transforms a partially received header
///     therefore cannot pass.
/// </remarks>
public sealed class StatefulHeaderFramer : INetFramer
{
    /// <summary>
    ///     Width in bytes of the obfuscated header.
    /// </summary>
    public const int HeaderLength = 4;

    private byte _key;
    private bool _headerDecoded;
    private int _expectedFrameLength;

    /// <summary>
    ///     Produces a frame the way the peer would send it, advancing <paramref name="key" /> exactly as
    ///     the framer will when it decodes.
    /// </summary>
    /// <param name="payload">
    ///     Payload bytes, sent in clear.
    /// </param>
    /// <param name="key">
    ///     Running key, advanced by <see cref="HeaderLength" />.
    /// </param>
    /// <returns>
    ///     The encoded frame: obfuscated header followed by the payload.
    /// </returns>
    public static byte[] Encode(ReadOnlySpan<byte> payload, ref byte key)
    {
        var frame = new byte[HeaderLength + payload.Length];
        BinaryPrimitives.WriteInt32BigEndian(frame, payload.Length);

        for (var i = 0; i < HeaderLength; i++)
        {
            frame[i] ^= key++;
        }

        payload.CopyTo(frame.AsSpan(HeaderLength));

        return frame;
    }

    public bool TryReadFrame(Span<byte> buffer, out int frameLength)
    {
        frameLength = 0;

        if (!_headerDecoded)
        {
            if (buffer.Length < HeaderLength)
            {
                return false;
            }

            for (var i = 0; i < HeaderLength; i++)
            {
                buffer[i] ^= _key++;
            }

            _expectedFrameLength = HeaderLength + BinaryPrimitives.ReadInt32BigEndian(buffer[..HeaderLength]);
            _headerDecoded = true;
        }

        if (_expectedFrameLength < HeaderLength || buffer.Length < _expectedFrameLength)
        {
            return false;
        }

        frameLength = _expectedFrameLength;
        _headerDecoded = false;

        return true;
    }
}
