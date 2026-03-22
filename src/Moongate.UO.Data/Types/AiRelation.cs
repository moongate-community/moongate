namespace Moongate.UO.Data.Types;

/// <summary>
/// Represents the coarse-grained relation a mobile AI should infer toward another mobile.
/// </summary>
public enum AiRelation : byte
{
    Friendly = 0x00,
    Neutral = 0x01,
    Hostile = 0x02
}
