namespace Moongate.Server.Ultima.Data.Spells;

/// <summary>
///     The props of a mobile that the spells of Magery set and the fight reads: how much armor a protection adds and until
///     when, and until when Reactive Armor sends a part of a melee blow back (as much as the Magery of its wearer gives). A time is whole seconds since 1970, as
///     <c>world.now()</c> gives.
/// </summary>
public static class MagicProps
{
    /// <summary>
    ///     Prop of a mobile: the armor rating a Protection or Arch Protection adds to what absorbs a blow.
    /// </summary>
    public const string ArmorBonus = "magic.armor";

    /// <summary>
    ///     Prop of a mobile: the time, in seconds since 1970, the <see cref="ArmorBonus" /> lasts to.
    /// </summary>
    public const string ArmorUntil = "magic.armor_until";

    /// <summary>
    ///     Prop of a mobile: the time, in seconds since 1970, Reactive Armor lasts to.
    /// </summary>
    public const string ReactiveUntil = "magic.reactive_until";

    /// <summary>
    ///     Prop of a mobile: true while Magic Reflection is on it. The first harmful spell aimed at the mobile that can be
    ///     reflected is turned back on its caster and the prop is gone. A death ends it.
    /// </summary>
    public const string Reflect = "magic.reflect";

    /// <summary>
    ///     Prop of a summoned creature: the time, in seconds since 1970, it goes away at. A creature that has it is a loan
    ///     of the spell, which nobody may keep by riding it or by leaving it in a stable.
    /// </summary>
    public const string SummonUntil = "summon.until";
}
