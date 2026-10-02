using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Core.Types.Geometry;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Server.Ultima.Types.Movement;
using Moongate.Ultima.Types;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Keeps the live mobiles in the world and in the sector grid of <see cref="ISectorService" />, moves them over
///     <see cref="IMovementService" />, and hands out virtual serials for hair and beard the first time they are needed,
///     counting up from <see cref="Serial.MinVirtual" /> as ModernUO does.
/// </summary>
public sealed class MobileService : IMobileService
{
    private const int StatCap = 225;
    private const int FollowersMax = 5;
    private const byte DirectionMask = 0x07;

    private readonly ConcurrentDictionary<Serial, Serial> _hair = new();
    private readonly ConcurrentDictionary<Serial, Serial> _beard = new();
    private readonly ConcurrentDictionary<Serial, MobileEntity> _inWorld = new();
    private readonly ConcurrentDictionary<Serial, byte> _deleted = new();
    private readonly IMovementService _movement;
    private readonly ISectorService _sectors;
    private readonly INpcSenseService? _senses;
    private readonly IRegionService? _regions;
    private readonly ILogger _logger = Log.ForContext<MobileService>();
    private long _nextVirtual = Serial.MinVirtual;

    public IReadOnlyCollection<Serial> InWorld => _inWorld.Keys.ToArray();

    public IReadOnlyCollection<MobileEntity> Mobiles => _inWorld.Values.ToArray();

    public MobileService(
        IMovementService movement,
        ISectorService sectors,
        INpcSenseService? senses = null,
        IRegionService? regions = null
    )
    {
        _regions = regions;
        _movement = movement;
        _sectors = sectors;
        _senses = senses;
    }

    public Serial HairSerial(Serial mobile)
    {
        return _hair.GetOrAdd(mobile, _ => NextVirtual());
    }

    public Serial BeardSerial(Serial mobile)
    {
        return _beard.GetOrAdd(mobile, _ => NextVirtual());
    }

    public void EnterWorld(MobileEntity mobile)
    {
        if (_inWorld.TryGetValue(mobile.Id, out var previous))
        {
            _sectors.Remove(previous);
        }

        _inWorld[mobile.Id] = mobile;
        _deleted.TryRemove(mobile.Id, out _);
        _sectors.Add(mobile);
        _senses?.Appeared(mobile);
        _regions?.Entered(mobile);
    }

    public bool TryGet(Serial serial, [NotNullWhen(true)] out MobileEntity? mobile)
    {
        return _inWorld.TryGetValue(serial, out mobile);
    }

    public bool LeaveWorld(Serial serial)
    {
        if (!_inWorld.TryRemove(serial, out var mobile))
        {
            return false;
        }

        _sectors.Remove(mobile);
        _regions?.Left(serial);

        return true;
    }

    public bool Delete(Serial serial)
    {
        if (!LeaveWorld(serial))
        {
            return false;
        }

        _deleted[serial] = 0;

        return true;
    }

    public IReadOnlyCollection<Serial> Capture()
    {
        return _deleted.Keys.ToArray();
    }

    public void Committed(IReadOnlyCollection<Serial> serials)
    {
        foreach (var serial in serials)
        {
            _deleted.TryRemove(serial, out _);
        }
    }

    public MoveResultType TryMove(
        MobileEntity mobile,
        DirectionType direction,
        MovementAbilityType ability = MovementAbilityType.Walk
    )
    {
        var facing = (DirectionType)((byte)direction & DirectionMask);

        if (facing != mobile.Direction)
        {
            mobile.Direction = facing;

            return MoveResultType.Turned;
        }

        int newZ;

        try
        {
            if (!_movement.CheckMovement(mobile.Map, mobile.Location, facing, ability, out newZ))
            {
                return MoveResultType.Blocked;
            }
        }
        catch (KeyNotFoundException exception)
        {
            _logger.Error(exception, "{Mobile} cannot move: its map is not loaded", mobile);

            return MoveResultType.Blocked;
        }

        var oldLocation = mobile.Location;
        var next = oldLocation.Move(facing);
        mobile.Location = new(next.X, next.Y, newZ);
        _sectors.Move(mobile);
        _senses?.Moved(mobile, oldLocation);
        _regions?.Moved(mobile);

        return MoveResultType.Moved;
    }

    public bool MoveTo(MobileEntity mobile, Point3D location)
    {
        if (!_inWorld.ContainsKey(mobile.Id) || !_sectors.IsInside(mobile.Map, location.X, location.Y))
        {
            return false;
        }

        var oldLocation = mobile.Location;
        mobile.Location = location;
        _sectors.Move(mobile);
        _senses?.Moved(mobile, oldLocation);
        _regions?.Moved(mobile);

        return true;
    }

    public bool IsInWorld(Serial mobile)
    {
        return _inWorld.ContainsKey(mobile);
    }

    public MobileFlagsType GetFlags(MobileEntity mobile)
    {
        return mobile.Gender == GenderType.Female ? MobileFlagsType.Female : MobileFlagsType.None;
    }

    public MobileStatusInfo GetStatus(MobileEntity mobile)
    {
        return new()
        {
            Serial = mobile.Id,
            Name = mobile.Name,
            Hits = mobile.Hits,
            HitsMax = mobile.HitsMax,
            Female = mobile.Gender == GenderType.Female,
            Strength = mobile.Strength,
            Dexterity = mobile.Dexterity,
            Intelligence = mobile.Intelligence,
            Stamina = mobile.Stamina,
            StaminaMax = mobile.StaminaMax,
            Mana = mobile.Mana,
            ManaMax = mobile.ManaMax,
            PhysicalResistance = mobile.ResistPhysical,
            Race = mobile.Race,
            StatCap = StatCap,
            FollowersMax = FollowersMax,
            FireResistance = mobile.ResistFire,
            ColdResistance = mobile.ResistCold,
            PoisonResistance = mobile.ResistPoison,
            EnergyResistance = mobile.ResistEnergy
        };
    }

    public List<MobileEquipmentEntry> GetEquipment(MobileEntity mobile, IEnumerable<ItemEntity> worn)
    {
        // The bank box is worn but never drawn: ModernUO leaves it out too.
        var entries = worn
            .Where(item => item.Layer is not null and not LayerType.Bank)
            .GroupBy(item => item.Layer!.Value)
            .Select(group => group.First())
            .Select(item => new MobileEquipmentEntry(item.Id, item.ItemId, item.Layer!.Value, item.Hue))
            .ToList();
        var layers = entries.Select(entry => entry.Layer).ToHashSet();

        if (mobile.HairStyle > 0 && layers.Add(LayerType.Hair))
        {
            entries.Add(new(HairSerial(mobile.Id), mobile.HairStyle, LayerType.Hair, mobile.HairHue));
        }

        if (mobile.BeardStyle > 0 && layers.Add(LayerType.FacialHair))
        {
            entries.Add(new(BeardSerial(mobile.Id), mobile.BeardStyle, LayerType.FacialHair, mobile.BeardHue));
        }

        return entries;
    }

    private Serial NextVirtual()
    {
        // The range holds about 17 million serials; wrap around rather than leave it.
        const long rangeSize = (long)Serial.MaxVirtual - Serial.MinVirtual + 1;
        var offset = (Interlocked.Increment(ref _nextVirtual) - 1 - Serial.MinVirtual) % rangeSize;

        return new Serial((uint)(Serial.MinVirtual + offset));
    }
}
