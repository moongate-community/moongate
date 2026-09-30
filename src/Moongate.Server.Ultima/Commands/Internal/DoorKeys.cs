using System.Diagnostics.CodeAnalysis;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Ultima.Commands.Internal;

/// <summary>
///     The doors of the lock commands: an item whose template runs <c>scripts/items/door.lua</c>, its linked door, and
///     the number (prop <c>key.value</c>) a key must carry to open it.
/// </summary>
internal static class DoorKeys
{
    public const string LockedProp = "locked";
    public const string KeyValueProp = "key.value";
    private const string DoorScript = "door";
    private const string LinkProp = "door.link";

    public static bool TryGetDoor(
        IItemService items,
        IItemTemplateService templates,
        Serial serial,
        [NotNullWhen(true)] out ItemEntity? door
    )
    {
        if (items.TryGet(serial, out var item) &&
            templates.TryGet(item.TemplateId, out var template) &&
            template.ScriptId == DoorScript)
        {
            door = item;

            return true;
        }

        door = null;

        return false;
    }

    /// <summary>
    ///     Gets the door linked to <paramref name="door" />, when it is a door too.
    /// </summary>
    public static ItemEntity? LinkedDoor(IItemService items, IItemTemplateService templates, ItemEntity door)
    {
        return door.Props?.GetValueOrDefault(LinkProp) is long link &&
               link is > 0 and <= uint.MaxValue &&
               TryGetDoor(items, templates, new Serial((uint)link), out var linked)
            ? linked
            : null;
    }

    /// <summary>
    ///     Gets the key number of the door, the one of its linked door, or a new random one, and gives it to both.
    /// </summary>
    public static long EnsureKeyValue(ItemEntity door, ItemEntity? linked)
    {
        var value = door.Props?.GetValueOrDefault(KeyValueProp) as long? ??
                    linked?.Props?.GetValueOrDefault(KeyValueProp) as long? ??
                    Random.Shared.NextInt64(1, uint.MaxValue);
        door.SetProp(KeyValueProp, value);
        linked?.SetProp(KeyValueProp, value);

        return value;
    }
}
