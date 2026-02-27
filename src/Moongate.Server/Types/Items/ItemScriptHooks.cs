namespace Moongate.Server.Types.Items;

/// <summary>
/// Canonical item script hook names used by the item script dispatcher.
/// </summary>
public static class ItemScriptHooks
{
    public const string OnUse = "on_use";

    public const string OnSingleClick = "on_single_click";

    public const string OnDoubleClick = "on_double_click";

    public const string OnDrop = "on_drop";

    public const string OnEquip = "on_equip";

    public const string OnUnequip = "on_unequip";
}
