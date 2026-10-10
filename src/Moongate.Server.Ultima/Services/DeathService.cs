using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Mounts;
using Moongate.Core.Types.Geometry;
using Moongate.Scripting.Interfaces;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Death;
using Moongate.Server.Ultima.Data.Internal.Death;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Data.Spells;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Server.Ultima.Types.Death;
using Moongate.Server.Ultima.Types.Templates;
using Moongate.Ultima.Types;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     The death of an NPC, in the order the other emulators keep: the corpse is made and filled, shown, the death is
///     played and heard, the script is told, the NPC is removed. The corpse is a ground container like any other: it
///     opens with a double click and decays by its template, with what is left inside.
/// </summary>
public sealed class DeathService : IDeathService
{
    public const int RemainsMessage = 30151;

    private const string DeathFunction = "on_death";
    private const string FallTimer = "npc-fall";
    private const int ByteMask = 0xFF;
    private const int DirectionMask = 0x07;

    /// <summary>
    ///     The item template of the shroud a ghost wears, on its outer torso layer.
    /// </summary>
    public const string ShroudTemplate = "death_shroud";

    /// <summary>
    ///     The item template of the robe a player wears when it is raised.
    /// </summary>
    public const string RobeTemplate = "death_robe";

    /// <summary>
    ///     The hit points of a player raised, as ServUO and ModernUO.
    /// </summary>
    public const int ResurrectedHits = 10;

    /// <summary>
    ///     The action a human body falls dead with, forward: the one the client draws its corpse lying in.
    /// </summary>
    public const int HumanFallAction = 21;

    /// <summary>
    ///     The frames of the fall asked of the client.
    /// </summary>
    public const int HumanFallFrames = 10;

    /// <summary>
    ///     How long the fall of a human body lasts on the client: its corpse takes its place when it is over.
    /// </summary>
    public static readonly TimeSpan FallTime = TimeSpan.FromMilliseconds(1500);

    private const int FirstMaleDeathSound = 0x15A;
    private const int FirstFemaleDeathSound = 0x150;
    private const int HumanDeathSounds = 4;

    // Where an item lands in a corpse when the container layouts are not there to say.
    private static readonly Point2D DefaultSpot = new(60, 110);

    private readonly IMobileService _mobiles;
    private readonly IItemService _items;
    private readonly IItemHandlingService _handling;
    private readonly IWorldViewService _view;
    private readonly ISpeechService _speech;
    private readonly INpcService _npcs;
    private readonly INpcScriptService _scripts;
    private readonly IMobileTemplateService _mobileTemplates;
    private readonly IItemTemplateService _itemTemplates;
    private readonly IGameLoopService _loop;
    private readonly Lazy<IScriptEngine> _engine;
    private readonly ITimerService _timers;
    private readonly IContainerLayoutService? _layouts;
    private readonly ICrimeService? _crimes;
    private readonly ILocalizationService? _localization;
    private readonly IMobileStateService? _state;
    private readonly INpcSenseService? _senses;
    private readonly IMurderService? _murders;
    private readonly IMountService? _mounts;
    private readonly Lazy<IPetService>? _pets;
    private readonly Lazy<ISpellCastService>? _casts;
    private readonly IParalysisService? _paralysis;
    private readonly IDisguiseService? _disguise;
    private readonly ILogger _logger;

    // Who is between its death and its removal: it does not die twice.
    private readonly HashSet<Serial> _dying = [];

    // The corpses someone is being raised from: nobody is raised twice from one.
    private readonly HashSet<Serial> _raising = [];

    public DeathService(
        IMobileService mobiles,
        IItemService items,
        IItemHandlingService handling,
        IWorldViewService view,
        ISpeechService speech,
        INpcService npcs,
        INpcScriptService scripts,
        IMobileTemplateService mobileTemplates,
        IItemTemplateService itemTemplates,
        IGameLoopService loop,
        Lazy<IScriptEngine> engine,
        ITimerService timers,
        IContainerLayoutService? layouts = null,
        ICrimeService? crimes = null,
        ILocalizationService? localization = null,
        IMobileStateService? state = null,
        INpcSenseService? senses = null,
        IMurderService? murders = null,
        ILogger? logger = null,
        IMountService? mounts = null,
        Lazy<IPetService>? pets = null,
        Lazy<ISpellCastService>? casts = null,
        IParalysisService? paralysis = null,
        IDisguiseService? disguise = null
    )
    {
        _paralysis = paralysis;
        _disguise = disguise;
        _casts = casts;
        _pets = pets;
        _mounts = mounts;
        _murders = murders;
        _state = state;
        _senses = senses;
        _mobiles = mobiles;
        _items = items;
        _handling = handling;
        _view = view;
        _speech = speech;
        _npcs = npcs;
        _scripts = scripts;
        _mobileTemplates = mobileTemplates;
        _itemTemplates = itemTemplates;
        _loop = loop;
        _engine = engine;
        _timers = timers;
        _layouts = layouts;
        _crimes = crimes;
        _localization = localization;
        _logger = logger ?? Log.ForContext<DeathService>();
    }

    public bool Kill(MobileEntity mobile, MobileEntity? killer = null)
    {
        if (!mobile.IsNpc)
        {
            // A disguise ends before the body is asked about: the body of an animal has no ghost to turn into.
            _disguise?.End(mobile);

            return KillPlayer(mobile, killer);
        }

        if (!_mobiles.IsInWorld(mobile.Id) || !_dying.Add(mobile.Id))
        {
            return false;
        }

        // The dead cast no more, whatever killed them.
        _casts?.Value.Cancel(mobile);
        EndPoison(mobile);
        EndSpells(mobile);

        // The dead are wanted no more: a body that still falls is not a criminal for the next guard.
        if (mobile.Criminal)
        {
            _crimes?.Pardon(mobile);
        }

        _logger.Information(
            "{Name:l} ({Serial:l}) died at {Location:l} of {Map}, killed by {Killer:l}",
            mobile.Name,
            mobile.Id,
            mobile.Location,
            mobile.Map,
            killer?.Name ?? "nobody"
        );
        _senses?.Killed(mobile, killer);

        if (DeathSound(mobile) is { } sound)
        {
            _speech.PlaySound(mobile, sound);
        }

        if (CorpseProps.IsHumanBody(mobile.Body))
        {
            Fall(mobile, killer);

            return true;
        }

        var corpse = MakeCorpse(mobile, killer);

        if (corpse is not null)
        {
            _view.ItemAppeared(corpse);
        }

        _view.MobileDied(mobile, corpse?.Id ?? default);
        Finish(mobile, corpse, killer);

        return true;
    }

    public bool IsDying(Serial mobile)
    {
        return _dying.Contains(mobile);
    }

    public bool Resurrect(MobileEntity player)
    {
        if (_state is null || !player.IsDead || !_mobiles.IsInWorld(player.Id))
        {
            return false;
        }

        _state.SetDead(player, false);
        _state.SetStats(
            player,
            new MobileStatsChange { Hits = Math.Min(ResurrectedHits, player.EffectiveHitsMax), Stamina = player.EffectiveStaminaMax, Mana = 0 }
        );

        foreach (var shroud in _items.GetWorn(player.Id).Where(item => item.TemplateId == ShroudTemplate).ToArray())
        {
            _view.WornItemRemoved(player, shroud);
            _view.OwnItemRemoved(player, shroud);
            _items.Absorb(shroud);
        }

        Wear(player, RobeTemplate, LayerType.OuterTorso);
        _logger.Information(
            "{Name:l} ({Serial:l}) is raised at {Location:l} of {Map}",
            player.Name,
            player.Id,
            player.Location,
            player.Map
        );

        return true;
    }

    public async Task<ResurrectResult> ResurrectAsync(Serial corpse, CancellationToken cancellationToken = default)
    {
        Raising? raising = null;
        MobileEntity? player = null;
        var refusal = ResurrectResultType.NotACorpse;
        await OnLoopAsync(
            () =>
            {
                // A ghost is raised where it stands, without a corpse.
                if (_mobiles.TryGet(corpse, out var ghost) && Resurrect(ghost))
                {
                    player = ghost;

                    return;
                }

                refusal = TryBegin(corpse, out raising);
            },
            cancellationToken
        );

        if (player is not null)
        {
            return new(ResurrectResultType.Raised, player);
        }

        if (raising is null)
        {
            return new(refusal, null);
        }

        MobileEntity npc;

        try
        {
            npc = await _npcs.SpawnAsync(raising.Template, raising.Map, raising.Location, raising.Props, cancellationToken);
        }
        catch
        {
            // Not awaited: a loop that is stopping takes no work, and the failure of the birth is what matters.
            _loop.TryPost(new LoopActionWorkItem(() => _raising.Remove(corpse)));

            throw;
        }

        var risen = false;
        await OnLoopAsync(() => risen = Rise(corpse, raising, npc), CancellationToken.None);

        return risen ? new(ResurrectResultType.Raised, npc) : new(ResurrectResultType.CannotBeRaised, null);
    }

    // A player dies where it stands, at once: the corpse with its gear and what its backpack held, then the ghost in
    // its place. The backpack, the hair and what cannot be lost stay with it.
    private static void EndPoison(MobileEntity mobile)
    {
        mobile.RemoveProp(PoisonService.LevelProp);
        mobile.RemoveProp(PoisonService.TicksProp);
    }

    // A death ends what a spell put on the mobile that does not outlast it: a paralysis, a disguise (before the corpse
    // is made, so the corpse is its own) and a Magic Reflection.
    private void EndSpells(MobileEntity mobile)
    {
        _paralysis?.Release(mobile);
        _disguise?.End(mobile);
        mobile.RemoveProp(MagicProps.Reflect);
    }

    private bool KillPlayer(MobileEntity player, MobileEntity? killer)
    {
        if (_state is null ||
            player.IsDead ||
            GhostBodies.GhostOf(player.Body) == player.Body ||
            !_mobiles.IsInWorld(player.Id))
        {
            return false;
        }

        // The dead cast no more, whatever killed them; a death ends a poison at once, a quick resurrection does not
        // bring it back.
        _casts?.Value.Cancel(player);
        EndPoison(player);
        EndSpells(player);

        // Read before the pardon: a criminal or a murderer is no innocent to loot.
        var innocent = !player.IsMurderer && !player.Criminal;

        if (player.Criminal)
        {
            _crimes?.Pardon(player);
        }

        _logger.Information(
            "{Name:l} ({Serial:l}) died at {Location:l} of {Map}, killed by {Killer:l}",
            player.Name,
            player.Id,
            player.Location,
            player.Map,
            killer?.Name ?? "nobody"
        );
        _senses?.Killed(player, killer);
        _murders?.Died(player);

        if (DeathSound(player) is { } sound)
        {
            _speech.PlaySound(player, sound);
        }

        // The mount is not loot: the rider is on foot, and its horse stands where it falls, before the corpse is made.
        _mounts?.Dismount(player);

        var corpse = MakeCorpse(player, killer);

        if (corpse is not null)
        {
            corpse.SetProp(CorpseProps.Owner, (long)player.Id.Value);
            corpse.SetProp(CorpseProps.Innocent, innocent);
            _view.ItemAppeared(corpse);
        }

        _state.SetWarMode(player, false);
        _view.MobileDied(player, corpse?.Id ?? default);
        _state.SetStats(player, new MobileStatsChange { Hits = 0, Stamina = 0, Mana = 0 });
        _state.SetDead(player, true);

        // The robe of the last time would keep the shroud off: it is gone, as ModernUO deletes it.
        foreach (var robe in _items.GetWorn(player.Id).Where(item => item.TemplateId == RobeTemplate).ToArray())
        {
            _view.OwnItemRemoved(player, robe);
            _items.Absorb(robe);
        }

        Wear(player, ShroudTemplate, LayerType.OuterTorso);

        return true;
    }

    // An item of a template put on the mobile and shown, when there is a template, a serial and the layer is free.
    private void Wear(MobileEntity mobile, string template, LayerType layer)
    {
        if (_items.GetWorn(mobile.Id).Any(item => item.Layer == layer) || _handling.Make(template) is not { } item)
        {
            return;
        }

        _items.Add([item]);
        _items.Equip(item, mobile.Id, layer);
        _view.WornItemChanged(mobile, item);
    }

    // On the game loop: what the corpse says of who died, read once; the corpse is marked so nobody else raises it.
    private ResurrectResultType TryBegin(Serial serial, out Raising? raising)
    {
        raising = null;

        if (!_items.TryGet(serial, out var corpse) ||
            corpse.ItemId != CorpseProps.Graphic ||
            corpse.Map is not { } map ||
            corpse.GroundLocation is not { } location ||
            !_items.IsLyingOnGround(corpse))
        {
            return ResurrectResultType.NotACorpse;
        }

        // Read as they are: a script may have put anything in the props.
        var props = corpse.Props;

        if (props?.GetValueOrDefault(CorpseProps.MobileTemplate) is not string template ||
            !_mobileTemplates.TryGet(template, out _) ||
            !_raising.Add(serial))
        {
            return ResurrectResultType.CannotBeRaised;
        }

        var born = props.Where(prop => prop.Key.StartsWith(CorpseProps.Kept + CorpseProps.SpawnProps, StringComparison.Ordinal))
            .ToDictionary(prop => prop.Key[CorpseProps.Kept.Length..], prop => prop.Value);

        // The corpse of a bonded pet: its owner and its pet props come back, when the owner has room for it.
        if (props.GetValueOrDefault(CorpseProps.PetOwner) is long or int)
        {
            var owner = Convert.ToInt64(props[CorpseProps.PetOwner]);

            // The count of the followers is made afresh: a pet raised a moment ago may not be in it yet.
            if (owner is <= 0 or > uint.MaxValue ||
                !_mobiles.TryGet(new Serial((uint)owner), out var master) ||
                _pets is null ||
                !FitsAfresh(_pets.Value, master, template))
            {
                _raising.Remove(serial);

                return ResurrectResultType.CannotBeRaised;
            }

            born[MountProps.Owner] = owner;

            foreach (var prop in props.Where(prop => prop.Key.StartsWith(CorpseProps.Kept + CorpseProps.PetProps, StringComparison.Ordinal)))
            {
                born[prop.Key[CorpseProps.Kept.Length..]] = prop.Value;
            }
        }

        raising = new(
            template,
            map,
            location,
            props.GetValueOrDefault(CorpseProps.Name) as string,
            props.GetValueOrDefault(CorpseProps.Direction) switch
            {
                int value  => value,
                long value => (int)(value & ByteMask),
                _          => null
            },
            born
        );

        return ResurrectResultType.Raised;
    }

    private static bool FitsAfresh(IPetService pets, MobileEntity master, string template)
    {
        pets.Changed(master.Id);

        return pets.Followers(master) + pets.SlotsOf(template) <= pets.MaxFollowers;
    }

    // On the game loop: who was born takes the name and the facing of who died, rises, and the corpse is gone. False
    // when who was born is gone already, removed or killed in the turn between its birth and this: nothing is shown
    // and the corpse stays.
    private bool Rise(Serial serial, Raising raising, MobileEntity npc)
    {
        _raising.Remove(serial);

        if (!_mobiles.TryGet(npc.Id, out var live) || !ReferenceEquals(live, npc))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(raising.Name))
        {
            npc.Name = raising.Name;
        }

        if (raising.Direction is { } direction && Enum.IsDefined((DirectionType)(direction & DirectionMask)))
        {
            npc.Direction = (DirectionType)(direction & DirectionMask);
        }

        // A pet comes back weak, as a player does, and is one follower more for its owner.
        if (raising.Props?.ContainsKey(MountProps.Owner) == true)
        {
            _state?.SetStats(npc, new MobileStatsChange { Hits = Math.Min(ResurrectedHits, npc.EffectiveHitsMax), Mana = 0 });

            if (npc.GetProp(MountProps.Owner, 0L) is > 0 and var master && master <= uint.MaxValue)
            {
                _pets?.Value.Changed(new Serial((uint)master));
            }
        }

        // Shown again as who it was: it was born with a name and a facing of its template.
        _view.MobileAppeared(npc);

        if (CorpseProps.IsHumanBody(npc.Body))
        {
            _view.MobileAnimated(npc, HumanFallAction, HumanFallFrames, 1, false);
        }

        // Gone meanwhile, as by decay: who was born stays.
        if (_items.TryGet(serial, out var corpse))
        {
            _view.ItemDisappeared(corpse);
            Absorb(corpse);
        }

        _logger.Information(
            "{Name:l} ({Serial:l}) is raised at {Location:l} of {Map}",
            npc.Name,
            npc.Id,
            npc.Location,
            npc.Map
        );

        return true;
    }

    // The item and everything inside it, at any depth.
    private void Absorb(ItemEntity item)
    {
        foreach (var content in _items.GetContents(item.Id).ToArray())
        {
            Absorb(content);
        }

        _items.Absorb(item);
    }

    private async Task OnLoopAsync(Action action, CancellationToken cancellationToken)
    {
        var work = new LoopActionWorkItem(action);
        await _loop.PostAsync(work, cancellationToken);
        await work.Completion;
    }

    // A human, elf or gargoyle body plays its fall itself and leaves its corpse when the fall is over. The death packet
    // (0xAF) is not used for it: ClassicUO takes the clothes off a mobile that dies by it, since 2019, so it falls
    // naked, while an action it is told to play is drawn dressed. UOX3 plays the same action for an NPC a guard kills.
    private void Fall(MobileEntity mobile, MobileEntity? killer)
    {
        // It neither walks nor is pushed while it falls.
        mobile.Frozen = true;
        _view.MobileAnimated(mobile, HumanFallAction, HumanFallFrames, 1);
        _timers.RegisterTimer(
            FallTimer,
            FallTime,
            () =>
            {
                // Removed while it fell, such as by the remove command: nothing is left to do.
                if (!_mobiles.TryGet(mobile.Id, out var live) || !ReferenceEquals(live, mobile))
                {
                    _dying.Remove(mobile.Id);

                    return;
                }

                var corpse = MakeCorpse(mobile, killer);

                if (corpse is not null)
                {
                    _view.ItemAppeared(corpse);
                }

                Finish(mobile, corpse, killer);
            }
        );
    }

    // The script is told and the NPC leaves the world.
    private void Finish(MobileEntity mobile, ItemEntity? corpse, MobileEntity? killer)
    {
        var corpseSerial = (long?)corpse?.Id.Value;
        var killerSerial = (long?)killer?.Id.Value;

        // A kill asked by a script, such as mobile.kill in an on_think: the engine runs no script inside another, so
        // on_death and the removal wait for the next turn of the game loop, in this order.
        if (_engine.Value.IsRunningScript)
        {
            _scripts.Queue(mobile, DeathFunction, corpseSerial, killerSerial);

            if (!_loop.TryPost(new LoopActionWorkItem(() => Remove(mobile))))
            {
                Remove(mobile);
            }
        }
        else
        {
            RunScript(mobile, corpseSerial, killerSerial);
            Remove(mobile);
        }
    }

    // While the NPC is still there to be read. A script that fails does not keep the NPC alive.
    private void RunScript(MobileEntity mobile, long? corpse, long? killer)
    {
        try
        {
            _scripts.Run(mobile, DeathFunction, corpse, killer);
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "{Function:l} of {Name:l} ({Serial:l}) failed", DeathFunction, mobile.Name, mobile.Id);
        }
    }

    private void Remove(MobileEntity mobile)
    {
        _npcs.Remove(mobile.Id);
        _dying.Remove(mobile.Id);

        // A pet that dies is one follower less for its owner.
        if (mobile.GetProp(MountProps.Owner, 0L) is > 0 and var owner && owner <= uint.MaxValue)
        {
            _pets?.Value.Changed(new Serial((uint)owner));
        }
    }

    // Null when the corpse template is missing or no serial is left: the NPC dies all the same, with what it carried.
    private ItemEntity? MakeCorpse(MobileEntity mobile, MobileEntity? killer)
    {
        if (_handling.Make(CorpseProps.Template) is not { } corpse)
        {
            _logger.Warning(
                "{Name:l} ({Serial:l}) leaves no corpse: no item template {Template:l}, or no serial left",
                mobile.Name,
                mobile.Id,
                CorpseProps.Template
            );

            return null;
        }

        corpse.Name = _localization.Text(RemainsMessage, "the remains of {0}", mobile.Name ?? "");
        corpse.Hue = mobile.SkinHue;
        corpse.SetProp(CorpseProps.Body, mobile.Body);

        if (!string.IsNullOrEmpty(mobile.Name))
        {
            corpse.SetProp(CorpseProps.Name, mobile.Name);
        }

        // Its spawn region and its home: who is raised from the corpse has them again.
        foreach (var (key, value) in mobile.Props ?? [])
        {
            if (key.StartsWith(CorpseProps.SpawnProps, StringComparison.Ordinal))
            {
                corpse.SetProp(CorpseProps.Kept + key, value);
            }
        }

        // A bonded pet is raised again as its owner's: the owner, and what makes it that pet, are kept.
        if (mobile.IsNpc && mobile.GetProp(MountProps.PetBonded, false) && mobile.GetProp(MountProps.Owner, 0L) > 0)
        {
            corpse.SetProp(CorpseProps.PetOwner, mobile.GetProp(MountProps.Owner, 0L));

            foreach (var (key, value) in mobile.Props ?? [])
            {
                if (key.StartsWith(CorpseProps.PetProps, StringComparison.Ordinal))
                {
                    corpse.SetProp(CorpseProps.Kept + key, value);
                }
            }
        }

        corpse.SetProp(CorpseProps.Direction, (int)mobile.Direction);

        if (mobile.TemplateId is { } template)
        {
            corpse.SetProp(CorpseProps.MobileTemplate, template);
        }

        if (killer is not null)
        {
            corpse.SetProp(CorpseProps.Killer, (long)killer.Id.Value);
            corpse.SetProp(CorpseProps.KillerName, killer.Name);
        }

        corpse.PlaceOnGround(mobile.Map, mobile.Location);
        _items.Add([corpse]);

        var worn = new List<string>();

        foreach (var item in Dropped(mobile))
        {
            // Read before the move takes it away.
            if (item.MobileId == mobile.Id && item.Layer is { } layer)
            {
                worn.Add($"{item.Id.Value}:{(int)layer}");
            }

            _items.MoveToContainer(item, corpse.Id, _layouts?.RandomGridPosition(CorpseProps.Graphic) ?? DefaultSpot);

            // A player stays in the world: its own client must lose what it lost.
            if (!mobile.IsNpc)
            {
                _view.OwnItemRemoved(mobile, item);
            }
        }

        if (worn.Count > 0)
        {
            corpse.SetProp(CorpseProps.Worn, string.Join(',', worn));
        }

        if (mobile.HairStyle > 0)
        {
            corpse.SetProp(CorpseProps.Hair, mobile.HairStyle);
            corpse.SetProp(CorpseProps.HairHue, (int)mobile.HairHue.Value);
        }

        if (mobile.BeardStyle > 0)
        {
            corpse.SetProp(CorpseProps.Beard, mobile.BeardStyle);
            corpse.SetProp(CorpseProps.BeardHue, (int)mobile.BeardHue.Value);
        }

        return corpse;
    }

    // What goes into the corpse: what the NPC wore and what lay in its backpack. The backpack itself, hair, the shop
    // and bank layers stay on the NPC and go with it, and so does what cannot move or is newbied or blessed.
    private List<ItemEntity> Dropped(MobileEntity mobile)
    {
        var dropped = new List<ItemEntity>();

        foreach (var worn in _items.GetWorn(mobile.Id))
        {
            if (worn.Layer == LayerType.Backpack)
            {
                dropped.AddRange(_items.GetContents(worn.Id).Where(Drops));
            }
            else if (worn.Layer is not (LayerType.Hair or LayerType.FacialHair or LayerType.Face or LayerType.Mount
                         or LayerType.ShopBuy or LayerType.ShopResale or LayerType.ShopSell or LayerType.Bank) &&
                     Drops(worn))
            {
                dropped.Add(worn);
            }
        }

        return dropped;
    }

    private bool Drops(ItemEntity item)
    {
        if (!_itemTemplates.TryGet(item.TemplateId, out var template))
        {
            return item.Movable != false;
        }

        // The item's own flag wins over its template's.
        return (item.Movable ?? template.Movable) != false &&
               template.LootType is not (LootType.Newbied or LootType.Blessed);
    }

    // The sound of its template, as UOX3's creature sounds; a human, elf or gargoyle body without one has the four
    // voices of its gender. Nothing for anything else.
    private int? DeathSound(MobileEntity mobile)
    {
        if (mobile.TemplateId is { } id &&
            _mobileTemplates.TryGet(id, out var template) &&
            template.Sounds?.Death is { } sound and > 0)
        {
            return sound;
        }

        if (!CorpseProps.IsHumanBody(mobile.Body))
        {
            return null;
        }

        return (mobile.Gender == GenderType.Female ? FirstFemaleDeathSound : FirstMaleDeathSound) +
               Random.Shared.Next(HumanDeathSounds);
    }
}
