namespace Moongate.Server.Ultima.Types.Effects;

/// <summary>
///     Where on a mobile's body the particles of an effect are shown (0xC7), as ModernUO's EffectLayer. Only the
///     Enhanced Client reads it.
/// </summary>
public enum EffectLayerType : byte
{
    Head = 0,
    RightHand = 1,
    LeftHand = 2,
    Waist = 3,
    LeftFoot = 4,
    RightFoot = 5,
    CenterFeet = 7,

    /// <summary>
    ///     No body part: the effect is not tied to one.
    /// </summary>
    None = 255
}
