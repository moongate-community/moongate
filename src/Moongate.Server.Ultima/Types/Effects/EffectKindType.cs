namespace Moongate.Server.Ultima.Types.Effects;

/// <summary>
///     How the client plays a graphic effect: the type byte of the effect packets (0xC0, 0xC7).
/// </summary>
public enum EffectKindType : byte
{
    /// <summary>Flies from the source to the target.</summary>
    Moving = 0,

    /// <summary>A lightning bolt striking the source.</summary>
    Lightning = 1,

    /// <summary>Stays at a point of the map.</summary>
    FixedLocation = 2,

    /// <summary>Stays on an object and follows it.</summary>
    FixedObject = 3
}
