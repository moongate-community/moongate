using System.Buffers.Binary;

namespace Moongate.UO.Data.Utils;

/// <summary>
/// Shared animation constants and validation helpers.
/// </summary>
public static class AnimationUtils
{
    private static readonly HashSet<int> _validClientAction3DAnimations =
    [
        6,
        21,
        32,
        33,
        100,
        101,
        102,
        103,
        104,
        105,
        106,
        107,
        108,
        109,
        110,
        111,
        112,
        113,
        114,
        115,
        116,
        117,
        118,
        119,
        120,
        121,
        123,
        124,
        125,
        126,
        127,
        128
    ];

    public const int BowAction = 32;
    public const int SaluteAction = 33;

    public const short DefaultFrameCount = 5;
    public const short DefaultRepeatCount = 1;
    public const bool DefaultForward = true;
    public const bool DefaultRepeat = false;
    public const byte DefaultDelay = 0;

    public static bool IsValidClientAction3DAnimation(int action)
        => _validClientAction3DAnimations.Contains(action);

    public static bool TryReadClientAction3D(ReadOnlySpan<byte> payload, out int action)
    {
        action = 0;

        if (payload.Length != 4)
        {
            return false;
        }

        action = BinaryPrimitives.ReadInt32BigEndian(payload);

        return true;
    }

    public static short ClampActionToPacket(int action)
        => (short)Math.Clamp(action, short.MinValue, short.MaxValue);
}
