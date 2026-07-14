using SquidStd.Network.Interfaces.Framing;

namespace Moongate.Network.Framing;

/// <summary>
/// Per-connection framer that resolves the UO connection-opening seed before delegating to
/// <see cref="UoPacketFramer" />. New clients open the login connection with a 0xEF login-seed
/// packet (a normal id-prefixed packet), but the game-server reconnect after the 0x8C redirect
/// opens with a raw 4-byte seed that carries no packet id. That seed is emitted as its own frame
/// so the seed handshake can consume it before the 0x91 game login is parsed.
/// Stateful: it MUST be created fresh per client (via the server's connection-pipeline factory),
/// never shared.
/// </summary>
public sealed class UoSeedFramer : INetFramer
{
    private const byte LoginSeedPacketId = 0xEF;
    private const int RawSeedLength = 4;

    private readonly UoPacketFramer _packetFramer;

    private bool _seedResolved;

    public UoSeedFramer()
    {
        _packetFramer = new();
    }

    public bool TryReadFrame(ReadOnlySpan<byte> buffer, out int frameLength)
    {
        frameLength = 0;

        if (buffer.IsEmpty)
        {
            return false;
        }

        if (!_seedResolved && buffer[0] != LoginSeedPacketId)
        {
            // Raw 4-byte game-server seed: no packet id, so it cannot go through the packet-length
            // table. Emit it as its own frame and let the seed handshake read it.
            if (buffer.Length < RawSeedLength)
            {
                return false;
            }

            _seedResolved = true;
            frameLength = RawSeedLength;

            return true;
        }

        // Either the connection opened with a 0xEF login-seed packet, or the raw seed is already
        // behind us; from here every frame is a normal id-prefixed UO packet.
        _seedResolved = true;

        return _packetFramer.TryReadFrame(buffer, out frameLength);
    }
}
