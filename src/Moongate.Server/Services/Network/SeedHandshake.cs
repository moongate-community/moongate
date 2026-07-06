using System.Buffers.Binary;
using Moongate.Server.Interfaces;
using Moongate.Server.Types;

namespace Moongate.Server.Services.Network;

/// <summary>
/// Drives the initial handshake: a 0xEF login-seed packet, or a raw 4-byte seed sent by
/// the client when it reconnects to the game port. Ported from moongatev2.
/// </summary>
public static class SeedHandshake
{
    private const byte LoginSeedPacketId = 0xEF;
    private const int RawSeedLength = 4;

    public static SeedHandshakeResultType Process(ISeedTarget target, ReadOnlySpan<byte> frame, out int consumed)
    {
        consumed = 0;

        if (target.State != SessionStateType.AwaitingSeed)
        {
            return SeedHandshakeResultType.PassThrough;
        }

        if (frame.IsEmpty)
        {
            return SeedHandshakeResultType.Reject;
        }

        if (frame[0] == LoginSeedPacketId)
        {
            target.SetState(SessionStateType.Login);

            return SeedHandshakeResultType.PassThrough;
        }

        if (frame.Length < RawSeedLength)
        {
            return SeedHandshakeResultType.Reject;
        }

        var seed = BinaryPrimitives.ReadUInt32BigEndian(frame);

        if (seed == 0)
        {
            return SeedHandshakeResultType.Reject;
        }

        target.SetSeed(seed);
        target.SetState(SessionStateType.Login);
        consumed = RawSeedLength;

        return SeedHandshakeResultType.Consumed;
    }
}
