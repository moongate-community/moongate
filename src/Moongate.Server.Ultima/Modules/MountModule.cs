using Moongate.Core.Primitives;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Ultima.Modules;

/// <summary>
///     The <c>mount</c> Lua module: what the scripts of the statuettes do to put a player on an ethereal mount.
/// </summary>
[ScriptModule("mount", "Puts a player on a mount, such as the ethereal horse of a statuette.")]
public sealed class MountModule
{
    private readonly IMountService _mounts;
    private readonly IMobileService _mobiles;
    private readonly IItemService _items;

    public MountModule(IMountService mounts, IMobileService mobiles, IItemService items)
    {
        _mounts = mounts;
        _mobiles = mobiles;
        _items = items;
    }

    /// <summary>
    ///     Puts the player on the ethereal mount of a statuette in its backpack; <c>mount.ride_ethereal(who, statuette)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Puts the player on the ethereal mount of the statuette, an item of a template with the tag mount_item that lies in the player's backpack: the statuette is gone and the mount is worn; getting off gives the statuette back. False, with the reason told to the player, when the statuette is not in its backpack or it already rides; false and silent for a ghost, an item that is no statuette, or a serial that is not a player or an item in the world."
    )]
    public bool RideEthereal(long player, long statuette)
    {
        return player is > 0 and <= uint.MaxValue &&
               _mobiles.TryGet(new Serial((uint)player), out var rider) &&
               !rider.IsNpc &&
               statuette is > 0 and <= uint.MaxValue &&
               _items.TryGet(new Serial((uint)statuette), out var item) &&
               _mounts.TryMountEthereal(rider, item);
    }
}
