using Moongate.Core.Primitives;
using Moongate.Server.Core.Interfaces.Services;
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
    private const int MemoryLimit = 256;

    // The status of a player is sent at every change of its stats: the pets are counted once in a while, not at each.
    private static readonly TimeSpan MemoryOf = TimeSpan.FromSeconds(2);

    private readonly IMobileService _mobiles;
    private readonly IItemService _items;
    private readonly ITamingService _taming;
    private readonly PetsConfig _config;
    private readonly TimeProvider _time;
    private readonly Lazy<IMobileStateService>? _state;
    private readonly ISessionService? _sessions;
    private readonly Dictionary<Serial, (int Count, DateTimeOffset At)> _counted = [];

    public int MaxFollowers => _config.MaxFollowers;

    public PetService(
        IMobileService mobiles,
        IItemService items,
        ITamingService taming,
        PetsConfig config,
        TimeProvider? time = null,
        Lazy<IMobileStateService>? state = null,
        ISessionService? sessions = null
    )
    {
        _mobiles = mobiles;
        _items = items;
        _taming = taming;
        _config = config;
        _time = time ?? TimeProvider.System;
        _state = state;
        _sessions = sessions;
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
            total += SlotsFor(player, mobile);
        }

        Remember(player.Id, total, now);

        return total;
    }

    public void Changed(Serial player)
    {
        _counted.Remove(player);

        if (_state is not null &&
            _sessions is not null &&
            _mobiles.TryGet(player, out var mobile) &&
            !mobile.IsNpc &&
            _sessions.TryGetByCharacterId(player, out var session))
        {
            _state.Value.SendStatus(session, mobile);
        }
    }

    public bool Release(MobileEntity player, MobileEntity creature)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(creature);

        if (player.IsNpc ||
            !creature.IsNpc ||
            !_mobiles.IsInWorld(creature.Id) ||
            creature.GetProp(MountProps.Owner, 0L) != player.Id.Value)
        {
            return false;
        }

        creature.RemoveProp(MountProps.Owner);
        creature.RemoveProp(MountProps.PetOrder);

        // Wild again, it belongs to the region it came from, which counts it once more.
        if (creature.TryGetProp<string>(MountProps.PetRegion, out var region))
        {
            creature.SetProp(SpawnRegionService.RegionProp, region);
            creature.RemoveProp(MountProps.PetRegion);
        }

        Changed(player.Id);

        return true;
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
        // It is the player's now, not its spawn's: the region brings another in its place, and gets it back if it is let go.
        if (creature.TryGetProp<string>(SpawnRegionService.RegionProp, out var region))
        {
            creature.SetProp(MountProps.PetRegion, region);
        }

        creature.RemoveProp(SpawnRegionService.RegionProp);
        _counted.Remove(player.Id);

        return PetResultType.Ok;
    }

    // What a mobile of the world counts for the player: a creature of its own, or the creature of its own that a rider
    // sits on, whoever the rider is.
    private int SlotsFor(MobileEntity player, MobileEntity mobile)
    {
        if (mobile.IsNpc)
        {
            return mobile.GetProp(MountProps.Owner, 0L) == player.Id.Value ? SlotsOf(mobile.TemplateId) : 0;
        }

        return _items.GetWornAt(mobile.Id, LayerType.Mount) is { } mount &&
               mount.GetProp(MountProps.PetOwner, 0L) == player.Id.Value &&
               mount.TryGetProp<string>(MountProps.PetTemplate, out var ridden)
            ? SlotsOf(ridden)
            : 0;
    }

    // The players who left are forgotten as the memory grows.
    private void Remember(Serial player, int count, DateTimeOffset now)
    {
        if (_counted.Count > MemoryLimit)
        {
            foreach (var old in _counted.Where(entry => now - entry.Value.At >= MemoryOf).Select(entry => entry.Key).ToArray())
            {
                _counted.Remove(old);
            }
        }

        _counted[player] = (count, now);
    }
}
