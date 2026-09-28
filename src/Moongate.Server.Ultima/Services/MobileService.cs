using System.Collections.Concurrent;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Keeps the mobiles in the world and hands out virtual serials for hair and beard the first time they are needed,
///     counting up from <see cref="Serial.MinVirtual" /> as ModernUO does.
/// </summary>
public sealed class MobileService : IMobileService
{
    private const int StatCap = 225;
    private const int FollowersMax = 5;

    private readonly ConcurrentDictionary<Serial, Serial> _hair = new();
    private readonly ConcurrentDictionary<Serial, Serial> _beard = new();
    private readonly ConcurrentDictionary<Serial, byte> _inWorld = new();
    private long _nextVirtual = Serial.MinVirtual;

    public IReadOnlyCollection<Serial> InWorld => _inWorld.Keys.ToArray();

    public Serial HairSerial(Serial mobile)
    {
        return _hair.GetOrAdd(mobile, _ => NextVirtual());
    }

    public Serial BeardSerial(Serial mobile)
    {
        return _beard.GetOrAdd(mobile, _ => NextVirtual());
    }

    public void EnterWorld(Serial mobile)
    {
        _inWorld[mobile] = 0;
    }

    public bool IsInWorld(Serial mobile)
    {
        return _inWorld.ContainsKey(mobile);
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
        var entries = worn
            .Where(item => item.Layer is not null)
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
