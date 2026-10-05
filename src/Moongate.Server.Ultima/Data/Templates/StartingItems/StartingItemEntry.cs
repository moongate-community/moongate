using Moongate.Core.Primitives;

namespace Moongate.Server.Ultima.Data.Templates.StartingItems;

/// <summary>
///     One item of a <see cref="StartingItemSet" />.
/// </summary>
public class StartingItemEntry
{
    /// <summary>
    ///     Item template ids; one is picked at random.
    /// </summary>
    public List<string> Items { get; set; } = [];

    /// <summary>
    ///     How many to give; unset is 1.
    /// </summary>
    public DiceSpec? Amount { get; set; }

    /// <summary>
    ///     The hue to give the item; unset keeps the item's own hue.
    /// </summary>
    public HueSpec? Hue { get; set; }

    /// <summary>
    ///     Puts the item on the character instead of in the backpack.
    /// </summary>
    public bool Equip { get; set; }

    /// <summary>
    ///     Whether the item stays with the character on death; unset leaves the server's default.
    /// </summary>
    public bool? Newbie { get; set; }
    /// <summary>
    ///     Optional readable text template applied once when this item is created.
    /// </summary>
    public string? BookTemplate { get; set; }

    /// <summary>
    ///     Explicit custom values required by the book template.
    /// </summary>
    public Dictionary<string, object?> BookValues { get; set; } = new(StringComparer.Ordinal);

}
