namespace Moongate.Server.Ultima.Data.Items;

/// <summary>
///     The keys of the item props the server itself reads; scripts may add any other key.
/// </summary>
public static class ItemPropKeys
{
    /// <summary>
    ///     What happens to the item when its owner dies, when it differs from the template's (a <c>LootType</c>).
    /// </summary>
    public const string LootType = "loot_type";

    /// <summary>
    ///     The charges of a wand or another charged item.
    /// </summary>
    public const string Charges = "charges";

    /// <summary>
    ///     The cliloc that names the item instead of its graphic's, as a sign with a text of the client.
    /// </summary>
    public const string LabelNumber = "label_number";

    /// <summary>
    ///     What a bank check is worth in gold: shown on its tooltip, and what cashing it gives.
    /// </summary>
    public const string BankWorth = "bank.worth";

    /// <summary>
    ///     The current durability of a weapon or armour.
    /// </summary>
    public const string Durability = "durability";

    /// <summary>
    ///     The durability of a weapon or armour when new.
    /// </summary>
    public const string MaxDurability = "max_durability";

    /// <summary>
    ///     The crafting quality, an <c>ItemQualityType</c>; an item without it is <c>Regular</c>.
    /// </summary>
    public const string Quality = "quality";

    /// <summary>
    ///     The serial value of the mobile that crafted the item.
    /// </summary>
    public const string CrafterId = "crafter_id";
}
