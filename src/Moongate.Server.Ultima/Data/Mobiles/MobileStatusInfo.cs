using Moongate.Core.Primitives;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Data.Mobiles;

/// <summary>
///     Everything the status bar of a player's own character shows (0x11, version 5).
/// </summary>
public sealed record MobileStatusInfo
{
    public required Serial Serial { get; init; }

    public required string Name { get; init; }

    public int Hits { get; init; }

    public int HitsMax { get; init; }

    /// <summary>
    ///     Gets whether the viewer may rename the mobile, as for a tamed pet.
    /// </summary>
    public bool CanBeRenamed { get; init; }

    public bool Female { get; init; }

    public int Strength { get; init; }

    public int Dexterity { get; init; }

    public int Intelligence { get; init; }

    public int Stamina { get; init; }

    public int StaminaMax { get; init; }

    public int Mana { get; init; }

    public int ManaMax { get; init; }

    public int Gold { get; init; }

    /// <summary>
    ///     Gets the physical resistance, or the armor rating before Age of Shadows.
    /// </summary>
    public int PhysicalResistance { get; init; }

    public int Weight { get; init; }

    public int MaxWeight { get; init; }

    public RaceType Race { get; init; }

    public int StatCap { get; init; }

    public int Followers { get; init; }

    public int FollowersMax { get; init; }

    public int FireResistance { get; init; }

    public int ColdResistance { get; init; }

    public int PoisonResistance { get; init; }

    public int EnergyResistance { get; init; }

    public int Luck { get; init; }

    public int DamageMin { get; init; }

    public int DamageMax { get; init; }

    public int TithingPoints { get; init; }
}
