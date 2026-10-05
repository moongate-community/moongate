using Moongate.Core.Geometry;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Death;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
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

    private const int FirstMaleDeathSound = 0x15A;
    private const int FirstFemaleDeathSound = 0x150;
    private const int HumanDeathSounds = 4;

    // Where an item lands in a corpse when the container layouts are not there to say.
    private static readonly Point2D DefaultSpot = new(60, 110);

    // Human, elf and gargoyle, male and female: the bodies that die with a voice of their own.
    private static readonly HashSet<int> HumanBodies = [0x190, 0x191, 0x25D, 0x25E, 0x29A, 0x29B];

    private readonly IMobileService _mobiles;
    private readonly IItemService _items;
    private readonly IItemHandlingService _handling;
    private readonly IWorldViewService _view;
    private readonly ISpeechService _speech;
    private readonly INpcService _npcs;
    private readonly INpcScriptService _scripts;
    private readonly IMobileTemplateService _mobileTemplates;
    private readonly IItemTemplateService _itemTemplates;
    private readonly IContainerLayoutService? _layouts;
    private readonly ILocalizationService? _localization;
    private readonly ILogger _logger;

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
        _layouts = layouts;
        _localization = localization;
        _logger = logger ?? Log.ForContext<DeathService>();
    }

    public bool Kill(MobileEntity mobile, MobileEntity? killer = null)
    {
        if (!mobile.IsNpc || !_mobiles.IsInWorld(mobile.Id))
        {
            return false;
        }

        var corpse = MakeCorpse(mobile, killer);

        if (corpse is not null)
        {
            _view.ItemAppeared(corpse);
        }

        _view.MobileDied(mobile, corpse?.Id ?? default);

        if (DeathSound(mobile) is { } sound)
        {
            _speech.PlaySound(mobile, sound);
        }

        // While the NPC is still there to be read.
        _scripts.Run(mobile, "on_death", (long?)corpse?.Id.Value, (long?)killer?.Id.Value);
        _npcs.Remove(mobile.Id);
        _logger.Information(
            "{Name:l} ({Serial:l}) died at {Location:l} of {Map}, killed by {Killer:l}",
            mobile.Name,
            mobile.Id,
            mobile.Location,
            mobile.Map,
            killer?.Name ?? "nobody"
        );

        return true;
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

        foreach (var item in Dropped(mobile))
        {
            _items.MoveToContainer(item, corpse.Id, _layouts?.RandomGridPosition(CorpseProps.Graphic) ?? DefaultSpot);
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
        if (item.Movable == false)
        {
            return false;
        }

        if (!_itemTemplates.TryGet(item.TemplateId, out var template))
        {
            return true;
        }

        return template.Movable != false && template.LootType is not (LootType.Newbied or LootType.Blessed);
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

        if (!HumanBodies.Contains(mobile.Body))
        {
            return null;
        }

        return (mobile.Gender == GenderType.Female ? FirstFemaleDeathSound : FirstMaleDeathSound) +
               Random.Shared.Next(HumanDeathSounds);
    }
}
