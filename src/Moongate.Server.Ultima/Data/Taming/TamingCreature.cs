namespace Moongate.Server.Ultima.Data.Taming;

/// <summary>
///     One <c>[[creature]]</c> of <c>data/taming.toml</c>: a creature a player can tame, with the skill it asks and the
///     followers it counts for once it is tamed.
/// </summary>
public class TamingCreature
{
    /// <summary>
    ///     The id of the mobile template of the creature, such as <c>horse</c>.
    /// </summary>
    public string Template { get; set; } = string.Empty;

    /// <summary>
    ///     The Animal Taming it takes, in points, such as 29.1; the chance of a try grows from 0.1 under it to 49.9 above.
    ///     -50 to 120: the small animals ask less than none.
    /// </summary>
    public double MinSkill { get; set; }

    /// <summary>
    ///     How many followers the creature counts for once it is tamed. 1 to 10.
    /// </summary>
    public int Slots { get; set; } = 1;

    /// <summary>
    ///     The kinds of food it eats once tamed: <c>meat</c>, <c>fruit</c>, <c>grain</c>, <c>fish</c> or <c>eggs</c>.
    ///     Meat when left out.
    /// </summary>
    public List<string> Food { get; set; } = ["meat"];
}
