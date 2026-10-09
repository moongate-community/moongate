using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Persistence.Interfaces;
using Moongate.Server.Ultima.Data.Mounts;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Items;
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
    private const int MountHeightReach = 8;
    private const int SpawnAttempts = 3;

    private readonly IMobileService _mobiles;
    private readonly IItemService _items;
    private readonly IItemHandlingService _handling;
    private readonly IWorldViewService _view;
    private readonly INpcService _npcs;
    private readonly IMobileTemplateService _templates;
    private readonly ISpeechService _speech;
    private readonly Lazy<IDeathService>? _death;
    private readonly IInventoryMutationGuard? _inventory;
    private readonly IDataAccess<ItemEntity>? _itemData;
    private readonly ILogger _logger;

    /// <summary>
    ///     Gets how long a failed spawn of the creature waits before it is tried again.
    /// </summary>
    internal TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(1);

    public MountService(
        IMobileService mobiles,
        IItemService items,
        IItemHandlingService handling,
        IWorldViewService view,
        INpcService npcs,
        IMobileTemplateService templates,
        ISpeechService speech,
        Lazy<IDeathService>? death = null,
        IInventoryMutationGuard? inventory = null,
        IDataAccess<ItemEntity>? itemData = null,
        ILogger? logger = null
    )
    {
        _death = death;
        _inventory = inventory;
        _itemData = itemData;
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
        // A creature that is dying, or that cannot be ridden, is no mount; a rider that cannot carry one gets none.
        if (!pet.IsNpc ||
            MountItemTemplate(pet) is not { } mountItem ||
            rider.IsDead ||
            _death?.Value.IsDying(pet.Id) == true ||
            _inventory?.AllowsOwner(rider.Id) == false)
        {
            return false;
        }

        if (IsMounted(rider))
        {
            _speech.TellCliloc(rider, AlreadyMountedCliloc);

            return false;
        }

        if (rider.Map != pet.Map || !WithinReach(rider.Location, pet.Location))
        {
            _speech.TellCliloc(rider, TooFarCliloc);

            return false;
        }

        // A wild creature is nobody's to ride, the staff's included: it is tamed first. The staff rides another's.
        var owner = pet.GetProp(MountProps.Owner, 0L);

        if (owner == 0)
        {
            _speech.TellCliloc(rider, NotYoursCliloc);

            return false;
        }

        if (!force && owner != rider.Id.Value)
        {
            _speech.TellCliloc(rider, SomeoneElsesCliloc);

            return false;
        }

        if (_handling.Make(mountItem) is not { } item)
        {
            _logger.Warning("Mount item {Item:l} of {Pet:l} does not exist", mountItem, pet.TemplateId);

            return false;
        }

        // The mount is not loot nor a gift: nobody lifts it off the rider, whatever its graphic weighs.
        item.Movable = false;
        item.SetProp(MountProps.PetTemplate, pet.TemplateId);
        item.SetProp(MountProps.PetOwner, owner);

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
        var mountSerial = item.Id;

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
        _ = Task.Run(() => SpawnAsync(mountSerial, template, map, location, props));

        return true;
    }

    private async Task SpawnAsync(
        Serial mountSerial,
        string template,
        MapType map,
        Point3D location,
        IReadOnlyDictionary<string, object?>? props
    )
    {
        // The row of the mount goes before the creature comes: a crash between the two must not leave both.
        await DeleteMountRowAsync(mountSerial);

        for (var attempt = 1; attempt <= SpawnAttempts; attempt++)
        {
            try
            {
                await _npcs.SpawnAsync(template, map, location, props);

                return;
            }
            catch (Exception exception)
            {
                _logger.Error(
                    exception,
                    "The {Template:l} of a rider could not be made again at {Location:l} (attempt {Attempt} of {Attempts})",
                    template,
                    location,
                    attempt,
                    SpawnAttempts
                );

                if (attempt < SpawnAttempts)
                {
                    await Task.Delay(RetryDelay);
                }
            }
        }
    }

    private async Task DeleteMountRowAsync(Serial mountSerial)
    {
        if (_itemData is null)
        {
            return;
        }

        try
        {
            await _itemData.DeleteAsync(mountSerial);
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "The mount item {Item:l} could not be deleted at once", mountSerial);
        }
    }

    private ItemEntity? MountItemOf(MobileEntity rider)
    {
        return _items.GetWornAt(rider.Id, LayerType.Mount);
    }

    private string? MountItemTemplate(MobileEntity pet)
    {
        return pet.TemplateId is { } id && _templates.TryGet(id, out var template) ? template.MountItem() : null;
    }

    internal static bool WithinReach(Point3D from, Point3D to)
    {
        return Math.Max(Math.Abs(from.X - to.X), Math.Abs(from.Y - to.Y)) <= MountRange &&
               Math.Abs(from.Z - to.Z) <= MountHeightReach;
    }
}
