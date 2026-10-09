using Moongate.Server.Ultima.Data.Harvest;
using Moongate.Server.Ultima.Data.Internal.Harvest;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Keeps the areas of each harvest resource in memory: drawn full at first use, and full again all at once some
///     time after the first take. An area of a resource with veins is of one of them, drawn each time it fills.
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

    public string? Vein(string resource, MapType map, int x, int y)
    {
        return AreaOf(resource, map, x, y, out _)?.Vein;
    }

    public bool TryTake(string resource, MapType map, int x, int y)
    {
        if (AreaOf(resource, map, x, y, out var definition) is not { Amount: > 0 } area)
        {
            return false;
        }

        area.Amount--;

        // Counted from the first take from a full area: fishing on does not push the refill away.
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
            area = _areas[key] = new();
            Fill(area, definition);
        }
        else if (area.RefillAt is { } due && _time.GetTimestamp() >= due)
        {
            Fill(area, definition);
        }

        return area;
    }

    // A full area: its amount, then its vein.
    private void Fill(HarvestArea area, HarvestResource definition)
    {
        area.Amount = definition.AmountMin + _random.Next(definition.AmountMax - definition.AmountMin + 1);
        area.RefillAt = null;
        area.Vein = VeinOf(definition);
    }

    // One of the veins of the resource, each as likely as its weight; none for a resource without veins.
    private string? VeinOf(HarvestResource definition)
    {
        if (definition.Vein.Count == 0)
        {
            return null;
        }

        var draw = _random.Next(definition.Vein.Sum(vein => vein.Weight));

        foreach (var vein in definition.Vein)
        {
            if (draw < vein.Weight)
            {
                return vein.Id;
            }

            draw -= vein.Weight;
        }

        return definition.Vein[^1].Id;
    }

    private long MinutesToTimestamp(int minutes)
    {
        return minutes * 60L * _time.TimestampFrequency;
    }
}
