namespace Moongate.UO.Data.Utils;

/// <summary>
/// Concrete packet-ready animation definition.
/// </summary>
public readonly record struct AnimationSpec(
    short Action,
    short FrameCount = AnimationUtils.DefaultFrameCount,
    short RepeatCount = AnimationUtils.DefaultRepeatCount,
    bool Forward = AnimationUtils.DefaultForward,
    bool Repeat = AnimationUtils.DefaultRepeat,
    byte Delay = AnimationUtils.DefaultDelay
);
