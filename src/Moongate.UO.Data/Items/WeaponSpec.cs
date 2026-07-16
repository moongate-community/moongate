namespace Moongate.UO.Data.Items;

/// <summary>Weapon attributes: damage range, speed, skill, range and ammo (for ranged weapons).</summary>
public sealed class WeaponSpec
{
    public int LowDamage { get; set; }
    public int HighDamage { get; set; }
    public int Speed { get; set; }
    public int BaseRange { get; set; }
    public int MaxRange { get; set; }
    public int HitSound { get; set; }
    public int MissSound { get; set; }
    public string? WeaponSkill { get; set; }
    public int? Ammo { get; set; }
    public int? AmmoFx { get; set; }
}
