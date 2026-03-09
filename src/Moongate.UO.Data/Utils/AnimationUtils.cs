using System.Buffers.Binary;
using Moongate.UO.Data.Types;

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

    public static bool TryResolveAnimation(
        AnimationIntent intent,
        UOBodyType bodyType,
        bool isMounted,
        out AnimationSpec animation
    )
    {
        animation = default;

        switch (intent)
        {
            case AnimationIntent.Bow:
                if (bodyType != UOBodyType.Human || isMounted)
                {
                    return false;
                }

                animation = new AnimationSpec((short)BowAction);

                return true;
            case AnimationIntent.Salute:
                if (bodyType != UOBodyType.Human || isMounted)
                {
                    return false;
                }

                animation = new AnimationSpec((short)SaluteAction);

                return true;
            case AnimationIntent.SwingPrimary:
                return TryResolveSwingAnimation(bodyType, isMounted, secondary: false, out animation);
            case AnimationIntent.SwingSecondary:
                return TryResolveSwingAnimation(bodyType, isMounted, secondary: true, out animation);
            case AnimationIntent.Hurt:
                return TryResolveHurtAnimation(bodyType, isMounted, out animation);
            default:
                return false;
        }
    }

    private static bool TryResolveHurtAnimation(UOBodyType bodyType, bool isMounted, out AnimationSpec animation)
    {
        animation = default;

        if (isMounted)
        {
            return false;
        }

        switch (bodyType)
        {
            case UOBodyType.Sea:
            case UOBodyType.Animal:
                animation = new AnimationSpec(Action: 7, FrameCount: 5);

                return true;
            case UOBodyType.Monster:
                animation = new AnimationSpec(Action: 10, FrameCount: 4);

                return true;
            case UOBodyType.Human:
                animation = new AnimationSpec(Action: 20, FrameCount: 5);

                return true;
            default:
                return false;
        }
    }

    private static bool TryResolveSwingAnimation(
        UOBodyType bodyType,
        bool isMounted,
        bool secondary,
        out AnimationSpec animation
    )
    {
        animation = default;

        if (isMounted && bodyType == UOBodyType.Human)
        {
            animation = new AnimationSpec(Action: secondary ? (short)29 : (short)26, FrameCount: 7);

            return true;
        }

        if (isMounted)
        {
            return false;
        }

        switch (bodyType)
        {
            case UOBodyType.Sea:
            case UOBodyType.Animal:
                animation = new AnimationSpec(Action: secondary ? (short)6 : (short)5, FrameCount: 7);

                return true;
            case UOBodyType.Monster:
                animation = new AnimationSpec(Action: secondary ? (short)5 : (short)4, FrameCount: 7);

                return true;
            case UOBodyType.Human:
                animation = new AnimationSpec(Action: secondary ? (short)10 : (short)9, FrameCount: 7);

                return true;
            default:
                return false;
        }
    }
}
