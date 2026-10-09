using Moongate.Core.Geometry;
using Moongate.Server.Ultima.Data.Mounts;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Ultima.Types;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Mounts and dismounts: see <see cref="IMountService" />.
/// </summary>
public sealed class MountService : IMountService
{
    private const int AlreadyMountedCliloc = 1005583;
    private const int TooFarCliloc = 500206;
    private const int NotYoursCliloc = 501263;
    private const int SomeoneElsesCliloc = 501264;
    private const int MountRange = 1;

    private readonly IMobileService _mobiles;
    private readonly IItemService _items;
    private readonly IItemHandlingService _handling;
    private readonly IWorldViewService _view;
    private readonly INpcService _npcs;
    private readonly IMobileTemplateService _templates;
    private readonly ISpeechService _speech;
    private readonly ILogger _logger;

    public MountService(
        IMobileService mobiles,
        IItemService items,
        IItemHandlingService handling,
        IWorldViewService view,
        INpcService npcs,
        IMobileTemplateService templates,
        ISpeechService speech,
        ILogger? logger = null
    )
    {
        _mobiles = mobiles;
        _items = items;
        _handling = handling;
        _view = view;
        _npcs = npcs;
        _templates = templates;
        _speech = speech;
        _logger = logger ?? Log.ForContext<MountService>();
    }

    public bool IsMounted(MobileEntity rider)
    {
        return MountItemOf(rider) is not null;
    }

    public bool TryMount(MobileEntity rider, MobileEntity pet, bool force = false)
    {
        if (!pet.IsNpc || MountItemTemplate(pet) is not { } mountItem || rider.IsDead)
        {
            return false;
        }

        if (IsMounted(rider))
        {
            _speech.TellCliloc(rider, AlreadyMountedCliloc);

            return false;
        }

        if (rider.Map != pet.Map || !WithinRange(rider.Location, pet.Location))
        {
            _speech.TellCliloc(rider, TooFarCliloc);

            return false;
        }

        var owner = pet.GetProp(MountProps.Owner, 0L);

        if (!force && owner != rider.Id.Value)
        {
            _speech.TellCliloc(rider, owner == 0 ? NotYoursCliloc : SomeoneElsesCliloc);

            return false;
        }

        if (_handling.Make(mountItem) is not { } item)
        {
            _logger.Warning("Mount item {Item:l} of {Pet:l} does not exist", mountItem, pet.TemplateId);

            return false;
        }

        item.SetProp(MountProps.PetTemplate, pet.TemplateId);

        if (owner != 0)
        {
            item.SetProp(MountProps.PetOwner, owner);
        }

        // Last: the creature goes only when the rider has its mount.
        if (!_npcs.Remove(pet.Id))
        {
            return false;
        }

        _items.Add([item]);
        _items.Equip(item, rider.Id, LayerType.Mount);
        _view.WornItemChanged(rider, item);

        return true;
    }

    public bool Dismount(MobileEntity rider)
    {
        if (MountItemOf(rider) is not { } item)
        {
            return false;
        }

        var template = item.GetProp<string?>(MountProps.PetTemplate, null);
        var owner = item.GetProp(MountProps.PetOwner, 0L);

        _view.OwnItemRemoved(rider, item);
        _view.WornItemRemoved(rider, item);
        _items.Absorb(item);

        if (template is null || !_mobiles.IsInWorld(rider.Id))
        {
            return true;
        }

        var map = rider.Map;
        var location = rider.Location;
        var props = owner == 0
            ? null
            : new Dictionary<string, object?> { [MountProps.Owner] = owner };

        // Off the loop: a new creature is saved first, to get its serial.
        _ = Task.Run(() => SpawnAsync(template, map, location, props));

        return true;
    }

    private async Task SpawnAsync(
        string template,
        MapType map,
        Point3D location,
        IReadOnlyDictionary<string, object?>? props
    )
    {
        try
        {
            await _npcs.SpawnAsync(template, map, location, props);
        }
        catch (Exception exception)
        {
            _logger.Error(
                exception,
                "The {Template:l} of a rider could not be made again at {Location:l}",
                template,
                location
            );
        }
    }

    private ItemEntity? MountItemOf(MobileEntity rider)
    {
        return _items.GetWorn(rider.Id).FirstOrDefault(item => item.Layer == LayerType.Mount);
    }

    private string? MountItemTemplate(MobileEntity pet)
    {
        return pet.TemplateId is { } id && _templates.TryGet(id, out var template) ? template.MountItem() : null;
    }

    private static bool WithinRange(Point3D from, Point3D to)
    {
        return Math.Max(Math.Abs(from.X - to.X), Math.Abs(from.Y - to.Y)) <= MountRange;
    }
}
