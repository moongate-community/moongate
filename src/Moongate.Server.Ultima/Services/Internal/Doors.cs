using Moongate.Server.Ultima.Commands.Internal;
using Moongate.Server.Ultima.Data.Tiles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Services.Internal;

/// <summary>
///     What the movement and the NPCs ask of a door lying on the ground.
/// </summary>
internal static class Doors
{
    /// <summary>
    ///     Gets whether a graphic is a door, as ModernUO: the tiledata Door flag, and the few door graphics that lack it.
    /// </summary>
    public static bool IsDoor(ItemTile tile)
    {
        return (tile.Flags & TileFlagType.Door) != 0 || tile.Id is 0x0692 or 0x0846 or 0x0873 or 0x06F5 or 0x06F6;
    }

    /// <summary>
    ///     Gets whether a door item is one an NPC may open on its way: closed, not locked, and of a template with a
    ///     script to open it. The path search and the NPC that opens ask the same question, so no path is planned
    ///     through a door nothing opens.
    /// </summary>
    public static bool CanBeOpened(ItemEntity door, IItemTemplateService? templates)
    {
        return templates is not null &&
               templates.TryGet(door.TemplateId, out var template) &&
               !string.IsNullOrEmpty(template.ScriptId) &&
               !(door.TryGetProp<bool>(DecorationService.OpenProp, out var open) && open) &&
               !(door.TryGetProp<bool>(DoorKeys.LockedProp, out var locked) && locked);
    }
}
