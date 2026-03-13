namespace Moongate.UO.Data.MegaCliloc;

/// <summary>
/// Common cliloc IDs used in MegaCliloc packets.
/// Keep this file conservative: values in the provisional section are not
/// verified against POL/UOX3 in this repository and should not be treated as
/// authoritative without packet/client validation.
/// </summary>
public static class CommonClilocIds
{
#region Verified And In Active Use

    /// <summary>
    /// Generic argument-based object name cliloc (~1_NOTHING~).
    /// POL uses this for item tooltip names and custom single-string entries.
    /// </summary>
    public const uint ObjectName = 1042971;

    /// <summary>
    /// Item name: ~1_NUMBER~ ~2_ITEMNAME~
    /// </summary>
    public const uint ItemName = 1050039;

    /// <summary>
    /// Amount: ~1_AMOUNT~
    /// </summary>
    public const uint Amount = 1062217;

    /// <summary>
    /// Weight: ~1_WEIGHT~ stones.
    /// Verified in UOX3 tooltip sender.
    /// </summary>
    public const uint Weight = 1072788;

    /// <summary>
    /// Artifact rarity ~1_val~
    /// </summary>
    public const uint ItemRarity = 1061078;

    /// <summary>
    /// Durability ~1_val~ / ~2_val~
    /// </summary>
    public const uint Durability = 1060639;

    /// <summary>
    /// Blessed
    /// </summary>
    public const uint Blessed = 1038021;

    /// <summary>
    /// Cursed
    /// </summary>
    public const uint Cursed = 1049643;

    /// <summary>
    /// Insured
    /// </summary>
    public const uint Insured = 1061682;

    /// <summary>
    /// Weapon Damage ~1_val~ - ~2_val~
    /// </summary>
    public const uint WeaponDamage = 1061168;

    /// <summary>
    /// Weapon Speed ~1_val~
    /// </summary>
    public const uint WeaponSpeed = 1061167;

    /// <summary>
    /// Hit Chance Increase ~1_val~%
    /// </summary>
    public const uint HitChanceIncrease = 1060415;

    /// <summary>
    /// Damage Increase ~1_val~%
    /// </summary>
    public const uint DamageIncrease = 1060401;

    /// <summary>
    /// Physical Resist ~1_val~%
    /// </summary>
    public const uint PhysicalResist = 1060448;

    /// <summary>
    /// Fire Resist ~1_val~%
    /// </summary>
    public const uint FireResist = 1060447;

    /// <summary>
    /// Cold Resist ~1_val~%
    /// </summary>
    public const uint ColdResist = 1060445;

    /// <summary>
    /// Poison Resist ~1_val~%
    /// </summary>
    public const uint PoisonResist = 1060449;

    /// <summary>
    /// Energy Resist ~1_val~%
    /// </summary>
    public const uint EnergyResist = 1060446;

    /// <summary>
    /// Armor Rating: ~1_val~.
    /// Kept because the current tooltip builder uses it, but verify client text
    /// before treating it as canonical AoS armor wording.
    /// </summary>
    public const uint ArmorRating = 1061170;

    /// <summary>
    /// Hit Points ~1_val~ / ~2_val~
    /// </summary>
    public const uint HitPoints = 1060578;

    /// <summary>
    /// Guild name: ~1_val~
    /// </summary>
    public const uint Guild = 1060802;

    /// <summary>
    /// Murderer (Red).
    /// In active use by the current mobile tooltip builder.
    /// </summary>
    public const uint Murderer = 1042735;

    /// <summary>
    /// Slayer: ~1_val~
    /// </summary>
    public const uint Slayer = 1060479;

    /// <summary>
    /// Spell Channeling
    /// </summary>
    public const uint SpellChanneling = 1060482;

    /// <summary>
    /// Skill Bonus: ~1_val~ +~2_val~
    /// </summary>
    public const uint SkillBonus = 1060451;

    /// <summary>
    /// Contents: ~1_val~ items, ~2_val~ stones
    /// </summary>
    public const uint ContainerContents = 1073841;

    /// <summary>
    /// Uses Remaining: ~1_val~
    /// </summary>
    public const uint UsesRemaining = 1060584;

#endregion

#region Provisional Or Not Verified Against POL Or UOX3

    /// <summary>
    /// Swing Speed Increase ~1_val~%
    /// </summary>
    public const uint SwingSpeedIncrease = 1060486;

    /// <summary>
    /// Mana ~1_val~ / ~2_val~
    /// </summary>
    public const uint Mana = 1060581;

    /// <summary>
    /// Stamina ~1_val~ / ~2_val~
    /// </summary>
    public const uint Stamina = 1060580;

    /// <summary>
    /// Strength ~1_val~
    /// </summary>
    public const uint Strength = 1060485;

    /// <summary>
    /// Dexterity ~1_val~
    /// </summary>
    public const uint Dexterity = 1060409;

    /// <summary>
    /// Intelligence ~1_val~
    /// </summary>
    public const uint Intelligence = 1060432;

    /// <summary>
    /// Taming Difficulty: ~1_val~
    /// </summary>
    public const uint TamingDifficulty = 1079860;

    /// <summary>
    /// Criminal (Gray)
    /// </summary>
    public const uint Criminal = 1042736;

    /// <summary>
    /// Karma: ~1_val~
    /// </summary>
    public const uint Karma = 1060659;

    /// <summary>
    /// Fame: ~1_val~
    /// </summary>
    public const uint Fame = 1060660;

    /// <summary>
    /// Magic Item
    /// </summary>
    public const uint MagicItem = 1060480;

    /// <summary>
    /// Faster Cast Recovery ~1_val~
    /// </summary>
    public const uint FasterCastRecovery = 1060412;

    /// <summary>
    /// Faster Casting ~1_val~
    /// </summary>
    public const uint FasterCasting = 1060413;

    /// <summary>
    /// Spell Damage Increase ~1_val~%
    /// </summary>
    public const uint SpellDamageIncrease = 1060483;

    /// <summary>
    /// Mana Regeneration ~1_val~
    /// </summary>
    public const uint ManaRegeneration = 1060440;

    /// <summary>
    /// Hit Point Regeneration ~1_val~
    /// </summary>
    public const uint HitPointRegeneration = 1060444;

    /// <summary>
    /// Stamina Regeneration ~1_val~
    /// </summary>
    public const uint StaminaRegeneration = 1060443;

    /// <summary>
    /// All Skills +~1_val~
    /// </summary>
    public const uint AllSkills = 1060366;

#endregion
}
