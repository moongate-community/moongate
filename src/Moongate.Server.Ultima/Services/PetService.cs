using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Mounts;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types.Pets;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     The creatures a player has tamed: see <see cref="IPetService" />.
/// </summary>
public sealed class PetService : IPetService
{
    // The status of a player is sent at every change of its stats: the pets are counted once in a while, not at each.
    private static readonly TimeSpan MemoryOf = TimeSpan.FromSeconds(2);

    private readonly IMobileService _mobiles;
    private readonly IItemService _items;
    private readonly ITamingService _taming;
    private readonly PetsConfig _config;
    private readonly TimeProvider _time;
    private readonly Dictionary<Serial, (int Count, DateTimeOffset At)> _counted = [];

    public int MaxFollowers => _config.MaxFollowers;

    public PetService(
        IMobileService mobiles,
        IItemService items,
        ITamingService taming,
        PetsConfig config,
        TimeProvider? time = null
    )
    {
        _mobiles = mobiles;
        _items = items;
        _taming = taming;
        _config = config;
        _time = time ?? TimeProvider.System;
    }

    public int Followers(MobileEntity player)
    {
        ArgumentNullException.ThrowIfNull(player);

        var now = _time.GetUtcNow();

        if (_counted.TryGetValue(player.Id, out var known) && now - known.At < MemoryOf && now >= known.At)
        {
            return known.Count;
        }

        var total = 0;

        foreach (var mobile in _mobiles.Mobiles)
        {
            if (mobile.IsNpc && mobile.GetProp(MountProps.Owner, 0L) == player.Id.Value)
            {
                total += SlotsOf(mobile.TemplateId);
            }
        }

        // The creature it rides is out of the world, kept on the mount item, but it is still a follower.
        if (_items.GetWornAt(player.Id, LayerType.Mount) is { } mount &&
            mount.TryGetProp<string>(MountProps.PetTemplate, out var ridden))
        {
            total += SlotsOf(ridden);
        }

        _counted[player.Id] = (total, now);

        return total;
    }

    public int SlotsOf(string? templateId)
    {
        return templateId is not null && _taming.TryGet(templateId, out var creature) ? creature.Slots : 1;
    }

    public PetResultType TryTame(MobileEntity player, MobileEntity creature)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(creature);

        if (player.IsNpc || !_mobiles.IsInWorld(player.Id))
        {
            return PetResultType.NoPlayer;
        }

        if (!creature.IsNpc || !_mobiles.IsInWorld(creature.Id))
        {
            return PetResultType.NotAnNpc;
        }

        if (creature.TemplateId is not { } templateId || !_taming.TryGet(templateId, out var entry))
        {
            return PetResultType.NotTamable;
        }

        if (creature.GetProp(MountProps.Owner, 0L) != 0)
        {
            return PetResultType.AlreadyOwned;
        }

        // Counted afresh: the memory of the count is not to let a sixth creature in.
        _counted.Remove(player.Id);

        if (Followers(player) + entry.Slots > _config.MaxFollowers)
        {
            return PetResultType.TooManyFollowers;
        }

        creature.SetProp(MountProps.Owner, (long)player.Id.Value);
        _counted.Remove(player.Id);

        return PetResultType.Ok;
    }
}
