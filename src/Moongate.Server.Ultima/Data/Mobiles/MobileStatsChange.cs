namespace Moongate.Server.Ultima.Data.Mobiles;

/// <summary>
///     The numbers of a mobile to change, each left null to keep what it is: its three stats, its hit points, mana
///     and stamina with their maximums, its fame and its karma.
/// </summary>
public sealed class MobileStatsChange
{
    public int? Strength { get; set; }

    public int? Dexterity { get; set; }

    public int? Intelligence { get; set; }

    public int? Hits { get; set; }

    public int? HitsMax { get; set; }

    public int? Mana { get; set; }

    public int? ManaMax { get; set; }

    public int? Stamina { get; set; }

    public int? StaminaMax { get; set; }

    public int? Fame { get; set; }

    public int? Karma { get; set; }
}
