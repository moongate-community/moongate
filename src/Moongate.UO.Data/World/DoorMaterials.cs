namespace Moongate.UO.Data.World;

/// <summary>
/// Decides which door belongs in a doorway from the wall it is cut into.
/// <para>
/// Both reference implementations skip this: ModernUO's generator hangs a dark wood door in every
/// doorway it finds, unconditionally, and moongatev2 ported that faithfully. But the client files
/// name every frame graphic by its material — <c>stone wall</c>, <c>wooden wall</c>, <c>marble
/// wall</c> — so the choice can be read off the map instead of invented. A bank gets a metal door
/// because it is built of stone, which is the same reason the artist drew it that way.
/// </para>
/// </summary>
public static class DoorMaterials
{
    /// <summary>What the references hang everywhere, and what an unrecognised wall still gets.</summary>
    public const string DefaultTemplateId = "dark_wood_door";

    private static readonly Dictionary<string, string> Doors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["wooden wall"] = "strong_wood_door",
        ["log wall"] = "strong_wood_door",
        ["stone wall"] = "metal_door",
        ["brick wall"] = "metal_door",
        ["sandstone wall"] = "metal_door",
        ["marble wall"] = DefaultTemplateId,
        ["plaster wall"] = DefaultTemplateId
    };

    /// <summary>
    /// Whether a gap beside this frame is a doorway at all.
    /// <para>
    /// Thirteen of the west frames are windows, and a window is a hole to look through rather than to
    /// walk through. The reference generators hang a door in every one of them — which nobody noticed
    /// while every generated door looked alike. This deliberately differs from them.
    /// </para>
    /// </summary>
    public static bool IsDoorway(string? wallName)
        => !string.Equals(wallName?.Trim(), "window", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The door template for a wall, by the name the client files give the frame graphic. A wall
    /// nobody mapped gets what the references give everything.
    /// </summary>
    public static string TemplateFor(string? wallName)
        => wallName is null ? DefaultTemplateId : Doors.GetValueOrDefault(wallName.Trim(), DefaultTemplateId);
}
