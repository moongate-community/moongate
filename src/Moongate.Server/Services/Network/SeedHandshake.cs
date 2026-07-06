using System.Buffers.Binary;
using Moongate.Server.Types;

namespace Moongate.Server.Services.Network;

/// <summary>
/// Drives the initial handshake: a 0xEF login-seed packet, or a raw 4-byte seed sent by
/// the client when it reconnects to the game port. Ported from moongatev2.
/// </summary>
public static class SeedHandshake
{
    private const byte LoginSeedPacketId = 0xEF;

    public static SeedHandshakeResult Process(ISeedTarget target, ReadOnlySpan<byte> frame, out int consumed)
    {
        consumed = 0;

        if (target.State != SessionStateType.AwaitingSeed)
        {
            return SeedHandshakeResult.PassThrough;
        }

        if (frame.IsEmpty)
        {
            return SeedHandshakeResult.Reject;
        }

        if (frame[0] == LoginSeedPacketId)
        {
            target.SetState(SessionStateType.Login);

            return SeedHandshakeResult.PassThrough;
        }

        if (frame.Length < 4)
        {
            return SeedHandshakeResult.Reject;
        }

        var seed = BinaryPrimitives.ReadUInt32BigEndian(frame);

        if (seed == 0)
        {
            return SeedHandshakeResult.Reject;
        }

        target.SetSeed(seed);
        target.SetState(SessionStateType.Login);
        consumed = 4;

        return SeedHandshakeResult.Consumed;
    }
}
