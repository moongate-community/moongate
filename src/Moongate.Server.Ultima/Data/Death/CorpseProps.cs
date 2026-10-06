namespace Moongate.Server.Ultima.Data.Death;

/// <summary>
///     What a corpse keeps in its props, and its graphic. A corpse is an item like any other: its amount stays 1, and
///     the body it shows travels to the client in the place of the amount.
/// </summary>
public static class CorpseProps
{
    /// <summary>
    ///     The graphic of every corpse; the client draws the body it is told in the place of the amount.
    /// </summary>
    public const int Graphic = 0x2006;

    /// <summary>
    ///     The id of the item template a corpse is made from.
    /// </summary>
    public const string Template = "corpse";

    /// <summary>
    ///     The body of who died, a whole number: the client draws the corpse with it.
    /// </summary>
    public const string Body = "corpse.body";

    /// <summary>
    ///     The way who died was facing, a DirectionType number: the corpse lies that way.
    /// </summary>
    public const string Direction = "corpse.direction";

    /// <summary>
    ///     The id of the mobile template of who died, when it had one.
    /// </summary>
    public const string MobileTemplate = "corpse.template";

    /// <summary>
    ///     The name of who died, so that who is raised from the corpse has it again.
    /// </summary>
    public const string Name = "corpse.name";

    /// <summary>
    ///     What comes before the name of each prop of its spawn region who died had, such as <c>spawn.region</c> and
    ///     the four of its home, kept as <c>corpse.spawn.region</c> and so on: who is raised from the corpse has them
    ///     again, counts for the region and keeps its home.
    /// </summary>
    public const string Kept = "corpse.";

    /// <summary>
    ///     The start of the props of a mobile that a corpse keeps.
    /// </summary>
    public const string SpawnProps = "spawn.";

    /// <summary>
    ///     The serial of who killed it, when someone did.
    /// </summary>
    public const string Killer = "corpse.killer";

    /// <summary>
    ///     The serial of the player the corpse is of; an NPC's corpse has none.
    /// </summary>
    public const string Owner = "corpse.owner";

    /// <summary>
    ///     Whether the player was an innocent when it died, that is no criminal and no murderer: taking from its corpse
    ///     is a crime.
    /// </summary>
    public const string Innocent = "corpse.innocent";

    /// <summary>
    ///     What who died wore that went into the corpse, as "serial:layer" pairs split by commas, the layer a LayerType
    ///     number: a human body is drawn wearing those still inside.
    /// </summary>
    public const string Worn = "corpse.worn";

    /// <summary>
    ///     The hair graphic of who died, and its hue; missing for none.
    /// </summary>
    public const string Hair = "corpse.hair";

    public const string HairHue = "corpse.hair_hue";

    /// <summary>
    ///     The beard graphic of who died, and its hue; missing for none.
    /// </summary>
    public const string Beard = "corpse.beard";

    public const string BeardHue = "corpse.beard_hue";

    /// <summary>
    ///     Gets whether the body is a human, elf or gargoyle one, male or female: the bodies whose corpse the client
    ///     draws dressed, and that die with a voice of their own.
    /// </summary>
    public static bool IsHumanBody(int body)
    {
        return body is 0x190 or 0x191 or 0x25D or 0x25E or 0x29A or 0x29B;
    }
}
