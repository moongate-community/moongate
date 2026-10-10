namespace Moongate.Server.Ultima.Data.Spells;

/// <summary>
///     The props of a mobile that the spells of Magery set and the fight reads: how much armor a protection adds and until
///     when, and how much of a melee blow Reactive Armor sends back and until when. A time is whole seconds since 1970, as
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
    ///     Prop of a mobile: the percent of a melee blow Reactive Armor sends back to whoever struck it.
    /// </summary>
    public const string ReactivePercent = "magic.reactive_percent";
}
