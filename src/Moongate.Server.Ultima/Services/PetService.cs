using Moongate.Core.Primitives;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Accounts;
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
    public const int MaxLoyalty = 100;

    private const int MemoryLimit = 256;

    // The creatures that ask this much Animal Taming or less always obey.
    private const double EasiestSkill = 29.1;

    // The status of a player is sent at every change of its stats: the pets are counted once in a while, not at each.
    private static readonly TimeSpan MemoryOf = TimeSpan.FromSeconds(2);

    private readonly IMobileService _mobiles;
    private readonly IItemService _items;
    private readonly ITamingService _taming;
    private readonly PetsConfig _config;
    private readonly IPetFoodService? _food;
    private readonly Func<double> _roll;
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
        ISessionService? sessions = null,
        IPetFoodService? food = null,
        Func<double>? roll = null
    )
    {
        _mobiles = mobiles;
        _items = items;
        _taming = taming;
        _config = config;
        _food = food;
        _roll = roll ?? Random.Shared.NextDouble;
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

        return LetGo(creature);
    }

    public bool LetGo(MobileEntity creature)
    {
        ArgumentNullException.ThrowIfNull(creature);

        var owner = creature.GetProp(MountProps.Owner, 0L);

        if (!creature.IsNpc || !_mobiles.IsInWorld(creature.Id) || owner == 0)
        {
            return false;
        }

        creature.RemoveProp(MountProps.Owner);
        creature.RemoveProp(MountProps.PetOrder);
        creature.RemoveProp(MountProps.PetLoyalty);

        // Wild again, it belongs to the region it came from, which counts it once more.
        if (creature.TryGetProp<string>(MountProps.PetRegion, out var region))
        {
            creature.SetProp(SpawnRegionService.RegionProp, region);
            creature.RemoveProp(MountProps.PetRegion);
        }

        Changed(new Serial((uint)owner));

        return true;
    }

    public int Loyalty(MobileEntity creature)
    {
        ArgumentNullException.ThrowIfNull(creature);

        return Math.Clamp(creature.GetProp(MountProps.PetLoyalty, MaxLoyalty), 0, MaxLoyalty);
    }

    public int AdjustLoyalty(MobileEntity creature, int delta)
    {
        var loyalty = Math.Clamp(Loyalty(creature) + delta, 0, MaxLoyalty);
        creature.SetProp(MountProps.PetLoyalty, loyalty);

        return loyalty;
    }

    public double ControlChance(MobileEntity player, MobileEntity creature)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(creature);

        if (IsStaff(player) ||
            creature.TemplateId is not { } template ||
            !_taming.TryGet(template, out var entry) ||
            entry.MinSkill <= EasiestSkill)
        {
            return 1;
        }

        // ModernUO's rule, in tenths of a point: a creature above the skill of the player weighs much more than one below.
        var minimum = (int)Math.Round(entry.MinSkill * 10);
        var taming = Skill(player, SkillType.AnimalTaming) - minimum;
        var lore = Skill(player, SkillType.AnimalLore) - minimum;
        var bonus = (taming * (taming >= 0 ? 6 : 28) + lore * (lore >= 0 ? 6 : 14)) / 2;
        var chance = Math.Clamp(700 + bonus, 220, 990) - (MaxLoyalty - Loyalty(creature)) * 10;

        return Math.Clamp(chance / 1000.0, 0, 1);
    }

    public PetObeyResultType Obey(MobileEntity player, MobileEntity creature)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(creature);

        if (player.IsNpc ||
            !creature.IsNpc ||
            !_mobiles.IsInWorld(creature.Id) ||
            creature.GetProp(MountProps.Owner, 0L) != player.Id.Value)
        {
            return PetObeyResultType.NotYours;
        }

        var chance = ControlChance(player, creature);

        if (chance >= 1 || _roll() < chance)
        {
            if (chance < 1)
            {
                AdjustLoyalty(creature, _config.ObeyGain);
            }

            return PetObeyResultType.Obeyed;
        }

        return AdjustLoyalty(creature, -_config.DisobeyLoss) <= 0 && LetGo(creature)
                   ? PetObeyResultType.Wild
                   : PetObeyResultType.Disobeyed;
    }

    public PetFeedResultType Feed(MobileEntity player, MobileEntity creature, string? itemTemplate, int amount)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(creature);

        if (player.IsNpc ||
            !creature.IsNpc ||
            !_mobiles.IsInWorld(creature.Id) ||
            creature.GetProp(MountProps.Owner, 0L) != player.Id.Value)
        {
            return PetFeedResultType.NotYours;
        }

        if (amount < 1 || _food?.Accepts(creature.TemplateId, itemTemplate) != true)
        {
            return PetFeedResultType.WrongFood;
        }

        if (Loyalty(creature) >= MaxLoyalty)
        {
            return PetFeedResultType.AlreadyHappy;
        }

        AdjustLoyalty(creature, (int)Math.Min((long)amount * _config.FoodGain, MaxLoyalty));

        return PetFeedResultType.Fed;
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

    // The skill of a player in tenths of a point, 0 for one it has not.
    private int Skill(MobileEntity player, SkillType type)
    {
        return _state?.Value.GetSkills(player).FirstOrDefault(skill => skill.Skill == type)?.Base ?? 0;
    }

    private bool IsStaff(MobileEntity player)
    {
        return _sessions is not null &&
               _sessions.TryGetByCharacterId(player.Id, out var session) &&
               session.AccountType >= AccountType.GameMaster;
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
