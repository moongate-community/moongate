namespace Moongate.UO.Data.World;

/// <summary>
/// Maps a declared decoration door class to the item template that gives it behaviour.
/// <para>
/// Every entry names a template that already ships with <c>ScriptId: items.door</c>, and whose own
/// <c>ItemId</c> is that class's base graphic — which is what lets the door script derive a door's
/// facing and its open graphic from nothing but the graphic it was placed with.
/// </para>
/// <para>
/// An explicit table rather than a naming convention: the corpus is upstream data, and a class nobody
/// has looked at should be placed as scenery rather than guessed into a door by the shape of its name.
/// </para>
/// </summary>
public static class DoorTemplates
{
    private static readonly Dictionary<string, string> Templates = new(StringComparer.Ordinal)
    {
        ["MetalDoor"] = "metal_door",
        ["MetalDoor2"] = "metal_door_2",
        ["BarredMetalDoor"] = "barred_metal_door",
        ["BarredMetalDoor2"] = "barred_metal_door_2",
        ["StrongWoodDoor"] = "strong_wood_door",
        ["DarkWoodDoor"] = "dark_wood_door",
        ["LightWoodDoor"] = "light_wood_door",
        ["RattanDoor"] = "rattan_door",
        ["SecretDungeonDoor"] = "secret_dungeon_door",
        ["SecretWoodenDoor"] = "secret_wooden_door",
        ["SecretStoneDoor1"] = "secret_stone_door_1",
        ["SecretStoneDoor2"] = "secret_stone_door_2",
        ["SecretStoneDoor3"] = "secret_stone_door_3"
    };

    /// <summary>The template a declared door class is built from, or null when it is not a door.</summary>
    public static string? For(string declaredType)
        => Templates.GetValueOrDefault(declaredType);

    /// <summary>Every template this map names, for a test that holds it against the shipped assets.</summary>
    public static IReadOnlyCollection<string> All => Templates.Values;
}
