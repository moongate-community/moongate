namespace Moongate.Server.Ultima.Data.Items;

/// <summary>
///     Values only some items have, stored in the <c>props</c> JSONB column of <c>world.items</c>. Adding a property
///     needs no migration; renaming one needs a data migration that rewrites the JSON.
/// </summary>
public sealed class ItemProps
{
    /// <summary>
    ///     Gets or sets the charges of a wand or another charged item.
    /// </summary>
    public int? Charges { get; set; }

    /// <summary>
    ///     Gets or sets the current durability of a weapon or armour.
    /// </summary>
    public int? Durability { get; set; }

    /// <summary>
    ///     Gets or sets the durability of a weapon or armour when new.
    /// </summary>
    public int? MaxDurability { get; set; }

    /// <summary>
    ///     Gets or sets the crafting quality, such as <c>exceptional</c>.
    /// </summary>
    public string? Quality { get; set; }

    /// <summary>
    ///     Gets or sets the serial value of the mobile that crafted the item.
    /// </summary>
    public uint? CrafterId { get; set; }

    /// <summary>
    ///     Gets or sets free values scripts attach to the item.
    /// </summary>
    public Dictionary<string, string>? Tags { get; set; }
}
