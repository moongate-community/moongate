using MessagePack;

namespace Moongate.Persistence.Data.Persistence;

[MessagePackObject(true)]
public sealed class ItemCombatStatsSnapshot
{
    public int MinStrength { get; set; }

    public int MinDexterity { get; set; }

    public int MinIntelligence { get; set; }

    public int DamageMin { get; set; }

    public int DamageMax { get; set; }

    public int Defense { get; set; }

    public int AttackSpeed { get; set; }

    public int RangeMin { get; set; }

    public int RangeMax { get; set; }

    public int MaxDurability { get; set; }

    public int CurrentDurability { get; set; }
}
