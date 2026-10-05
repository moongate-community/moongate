using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Scripting.Interfaces;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Death;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services.Internal;
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
    private readonly ILocalizationService? _localization;
    private readonly ILogger _logger;

    // Who is between its death and its removal: it does not die twice.
    private readonly HashSet<Serial> _dying = [];

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
        ILocalizationService? localization = null,
        ILogger? logger = null
    )
    {
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
        _localization = localization;
        _logger = logger ?? Log.ForContext<DeathService>();
    }

    public bool Kill(MobileEntity mobile, MobileEntity? killer = null)
    {
        if (!mobile.IsNpc || !_mobiles.IsInWorld(mobile.Id) || !_dying.Add(mobile.Id))
        {
            return false;
        }

        _logger.Information(
            "{Name:l} ({Serial:l}) died at {Location:l} of {Map}, killed by {Killer:l}",
            mobile.Name,
            mobile.Id,
            mobile.Location,
            mobile.Map,
            killer?.Name ?? "nobody"
        );

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
        corpse.SetProp(CorpseProps.Direction, (int)mobile.Direction);

        if (mobile.TemplateId is { } template)
        {
            corpse.SetProp(CorpseProps.MobileTemplate, template);
        }

        if (killer is not null)
        {
            corpse.SetProp(CorpseProps.Killer, (long)killer.Id.Value);
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
