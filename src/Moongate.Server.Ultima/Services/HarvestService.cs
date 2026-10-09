using Moongate.Server.Ultima.Data.Harvest;
using Moongate.Server.Ultima.Data.Internal.Harvest;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Keeps the areas of each harvest resource in memory, as ModernUO's harvest banks: drawn full at first use, and
///     full again all at once some time after the first take.
/// </summary>
public sealed class HarvestService : IHarvestService
{
    private readonly Lazy<Dictionary<string, HarvestResource>> _resources;
    private readonly Dictionary<(string Resource, MapType Map, int X, int Y), HarvestArea> _areas = [];
    private readonly TimeProvider _time;
    private readonly Random _random;

    public HarvestService(IDataLoaderService data, TimeProvider? time = null, Random? random = null)
    {
        _resources = new(() => data.GetEntities<HarvestResource>()
            .ToDictionary(resource => resource.Id, StringComparer.Ordinal)
        );
        _time = time ?? TimeProvider.System;
        _random = random ?? Random.Shared;
    }

    public bool Has(string resource)
    {
        return resource is not null && _resources.Value.ContainsKey(resource);
    }

    public int? Amount(string resource, MapType map, int x, int y)
    {
        return AreaOf(resource, map, x, y, out _)?.Amount;
    }

    public bool TryTake(string resource, MapType map, int x, int y)
    {
        if (AreaOf(resource, map, x, y, out var definition) is not { Amount: > 0 } area)
        {
            return false;
        }

        area.Amount--;

        // Counted from the first take from a full area, as ModernUO: fishing on does not push the refill away.
        area.RefillAt ??= _time.GetTimestamp() + MinutesToTimestamp(
            definition!.RespawnMinMinutes + _random.Next(definition.RespawnMaxMinutes - definition.RespawnMinMinutes + 1)
        );

        return true;
    }

    // The area of a cell, drawn full the first time and again once its refill is due.
    private HarvestArea? AreaOf(string resource, MapType map, int x, int y, out HarvestResource? definition)
    {
        definition = null;

        if (resource is null || x < 0 || y < 0 || !_resources.Value.TryGetValue(resource, out definition))
        {
            return null;
        }

        var key = (resource, map, x / definition.Area, y / definition.Area);

        if (!_areas.TryGetValue(key, out var area))
        {
            area = _areas[key] = new() { Amount = Full(definition) };
        }
        else if (area.RefillAt is { } due && _time.GetTimestamp() >= due)
        {
            area.Amount = Full(definition);
            area.RefillAt = null;
        }

        return area;
    }

    private int Full(HarvestResource definition)
    {
        return definition.AmountMin + _random.Next(definition.AmountMax - definition.AmountMin + 1);
    }

    private long MinutesToTimestamp(int minutes)
    {
        return minutes * 60L * _time.TimestampFrequency;
    }
}
