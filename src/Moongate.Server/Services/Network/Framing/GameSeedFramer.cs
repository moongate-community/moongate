using Moongate.Network.Interfaces.Framing;
using Moongate.Network.Packets.Registry;

namespace Moongate.Server.Services.Network.Framing;

/// <summary>
///     Reads the game connection seed before delegating subsequent frames to the UO packet framer.
/// </summary>
public sealed class GameSeedFramer : INetFramer
{
    private const int RawSeedLength = 4;
    private const byte VersionedSeedOpCode = 0xEF;

    private readonly UoPacketFramer _packets;
    private bool _seedRead;

    public GameSeedFramer(PacketRegistry registry)
    {
        _packets = new(registry);
    }

    public bool TryReadFrame(Span<byte> buffer, out int frameLength)
    {
        frameLength = 0;

        if (_seedRead)
        {
            return _packets.TryReadFrame(buffer, out frameLength);
        }

        if (buffer.IsEmpty)
        {
            return false;
        }

        if (buffer[0] == VersionedSeedOpCode)
        {
            if (!_packets.TryReadFrame(buffer, out frameLength))
            {
                return false;
            }

            _seedRead = true;

            return true;
        }

        if (buffer.Length < RawSeedLength)
        {
            return false;
        }

        if (buffer[..RawSeedLength].IndexOfAnyExcept((byte)0) < 0)
        {
            throw new InvalidDataException("A game connection cannot use a zero seed.");
        }

        frameLength = RawSeedLength;
        _seedRead = true;

        return true;
    }
}
