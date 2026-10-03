using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Internal.Spawns;
using Moongate.Server.Ultima.Data.Spawns;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Data.Templates.Spawns;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Server.Ultima.Speech;
using Moongate.Server.Ultima.Types.Items;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Ultima.Types;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     UOX3's <c>DoRegionSpawn</c>: the <c>npc_spawn</c> timer checks the regions every 10 seconds, and a due region below
///     its max spawns up to <c>call</c> NPCs, then waits between its min and max minutes. The first spawn of each region
///     comes within its min time (at most 10 minutes), so the world fills gradually. The live NPCs of a region are the
///     ones carrying its id in the prop <c>spawn.region</c>, counted at every check: an NPC removed or killed frees its
///     slot. After a check that spawned something, the game masters and administrators get one summary message.
///     A region of items works the same way with the items lying on the ground that carry its id, such as a treasure
///     chest until it decays; the staff is not told of them and the world progress is of NPCs only.
/// </summary>
public sealed class SpawnRegionService : ISpawnRegionService, IDisposable
{
    public const string TimerName = "npc_spawn";
    public const string RegionProp = "spawn.region";
    private const int DefaultPrefZ = 18;
    private const int SpotTries = 100;
    private const int RoofHeight = 10;
    private const int FirstSpawnWindowMinutes = 10;
    private const int NamedRegions = 5;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(1);
    private static readonly Hue NoticeHue = new(0x03B2);

    private readonly ILogger _logger = Log.ForContext<SpawnRegionService>();
    private readonly List<SpawnRegionState> _regions = [];
    private readonly CancellationTokenSource _stopping = new();
    private readonly Dictionary<string, MobileMovementType> _movements = new(StringComparer.Ordinal);
    private readonly IDataLoaderService _data;
    private readonly IMapService _map;
    private readonly IMovementService _movement;
    private readonly INpcService _npcs;
    private readonly IMobileService _mobiles;
    private readonly ISessionService _sessions;
    private readonly IPacketSendService _sender;
    private readonly ILocalizationService _localization;
    private readonly ITimerService _timers;
    private readonly IGameLoopService _loop;
    private readonly TimeProvider _time;
    private readonly Random _random;
    private readonly SpawnsConfig _config;
    private readonly IItemSpawnService? _itemSpawns;
    private readonly IItemService? _items;
    private readonly ISectorService? _sectors;

    // The items the regions spawned and that may still lie on the ground, by serial, with their region: found once
    // among the items of the world, then kept up to date, so a check does not go through every item.
    private Dictionary<Serial, string>? _spawnedItems;

    private string? _timer;

    /// <summary>
    ///     Gets the spawns started by the last check, done once they are all in the world and the staff was told.
    /// </summary>
    internal Task Running { get; private set; } = Task.CompletedTask;

    public SpawnRegionService(
        IDataLoaderService data,
        IMapService map,
        IMovementService movement,
        INpcService npcs,
        IMobileService mobiles,
        ISessionService sessions,
        IPacketSendService sender,
        ILocalizationService localization,
        ITimerService timers,
        IGameLoopService loop,
        TimeProvider time,
        Random? random = null,
        SpawnsConfig? config = null,
        IItemSpawnService? itemSpawns = null,
        IItemService? items = null,
        ISectorService? sectors = null
    )
    {
        _config = config ?? new();
        _itemSpawns = itemSpawns;
        _items = items;
        _sectors = sectors;
        _data = data;
        _map = map;
        _movement = movement;
        _npcs = npcs;
        _mobiles = mobiles;
        _sessions = sessions;
        _sender = sender;
        _localization = localization;
        _timers = timers;
        _loop = loop;
        _time = time;
        _random = random ?? Random.Shared;
    }

    public Task StartAsync()
    {
        var lists = _data.GetEntities<NpcListTemplate>().ToDictionary(list => list.Id, StringComparer.Ordinal);

        // As UOX3: a water mobile spawns on the water, one moving on both on land or else on the water.
        foreach (var mobile in _data.GetEntities<MobileTemplate>())
        {
            if (mobile.Movement is { } movement and not MobileMovementType.Land)
            {
                _movements[mobile.Id] = movement;
            }
        }
        var now = _time.GetUtcNow();
        var skipped = 0;

        foreach (var spawn in _data.GetEntities<SpawnTemplate>())
        {
            if (!_map.Maps.Contains(spawn.Map))
            {
                skipped++;

                continue;
            }

            var window = Math.Min(spawn.MinMinutes, FirstSpawnWindowMinutes) * 60;
            _regions.Add(
                new()
                {
                    Template = spawn,
                    Pool = spawn.ItemIds.Count > 0 ? null : new(spawn, lists),
                    NextSpawn = now + TimeSpan.FromSeconds(_random.Next(0, window + 1))
                }
            );
        }

        _timer = _timers.RegisterTimer(TimerName, CheckInterval, Check, CheckInterval, true);
        _logger.Information(
            "Spawning {Regions} regions for up to {Npcs} NPCs and {Items} items ({Skipped} on maps not loaded)",
            _regions.Count,
            _regions.Where(region => !region.OfItems).Sum(region => region.Template.Max),
            _regions.Where(region => region.OfItems).Sum(region => region.Template.Max),
            skipped
        );

        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (_timer is not null)
        {
            _timers.UnregisterTimer(_timer);
            _timer = null;
        }

        // Stopped before the world save stops the game loop: the spawn under way ends, the ones still to come never start.
        await _stopping.CancelAsync();

        try
        {
            await Running;
        }
        catch (OperationCanceledException)
        {
            // The spawn under way was cancelled.
        }
    }

    public async Task<IReadOnlyList<SpawnRegionStatus>> RegionsAtAsync(MapType map, int x, int y)
    {
        IReadOnlyList<SpawnRegionStatus> here = [];
        await OnLoopAsync(
            () =>
            {
                var now = _time.GetUtcNow();
                var live = CountLive();
                here = _regions.Where(region => region.Template.Map == map && region.Template.Areas.Any(area => area.Contains(x, y)))
                               .Select(
                                   region => new SpawnRegionStatus(
                                       region.Template.Id,
                                       region.Template.Name,
                                       live.GetValueOrDefault(region.Template.Id),
                                       region.Template.Max,
                                       region.NextSpawn > now ? region.NextSpawn - now : TimeSpan.Zero,
                                       region.Retrying
                                   )
                               )
                               .ToList();
            }
        );

        return here;
    }

    public async Task<(int Regions, int Missing)> FillAllAsync()
    {
        var regions = 0;
        var missing = 0;
        await OnLoopAsync(
            () =>
            {
                var now = _time.GetUtcNow();
                var live = CountLive();

                foreach (var region in _regions)
                {
                    region.FillNow = true;
                    region.NextSpawn = now;
                    missing += Math.Max(0, region.Template.Max - live.GetValueOrDefault(region.Template.Id));
                }

                regions = _regions.Count;
            }
        );

        return (regions, missing);
    }

    // On the game loop. The spawns themselves run off it, as INpcService asks; no check starts before they are done.
    private void Check()
    {
        if (!Running.IsCompleted)
        {
            return;
        }

        try
        {
            var now = _time.GetUtcNow();
            var live = CountLive();
            var planned = new List<PlannedSpawn>();

            foreach (var region in _regions)
            {
                if (region.NextSpawn <= now)
                {
                    Plan(region, now, live, planned);
                }
            }

            if (planned.Count > 0)
            {
                Running = Task.Run(() => SpawnAsync(planned, _stopping.Token));
            }
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "The spawn check failed");
        }
    }

    // A region that throws is retried a minute later; the others go on.
    private void Plan(SpawnRegionState region, DateTimeOffset now, Dictionary<string, int> live, List<PlannedSpawn> planned)
    {
        var template = region.Template;

        try
        {
            var room = template.Max - live.GetValueOrDefault(template.Id);

            // The first spawn after the start fills the region; later ones bring call NPCs at a time.
            var count = region.FillNow || _config.InitialFill && !region.Filled ? room : Math.Min(template.Call, room);
            var found = 0;

            // A pick with no spot, such as a sea creature in a region with little water, leaves the others to spawn.
            for (var i = 0; i < count; i++)
            {
                var templateId = region.Pool?.Pick(_random) ?? template.ItemIds[_random.Next(0, template.ItemIds.Count)];
                var movement = _movements.GetValueOrDefault(templateId, MobileMovementType.Land);

                // A spawned item wants a cell of its own: not where another one lies or is about to.
                Func<int, int, bool>? taken = region.OfItems
                    ? (x, y) => IsTaken(template.Map, x, y, planned) || HasSpawnedItem(template.Map, x, y)
                    : null;

                if (TryFindSpot(template, movement, taken, out var location, out var area))
                {
                    planned.Add(new(template, templateId, location, area, region.OfItems));
                    found++;
                }
            }

            var missed = count > 0 && found == 0;

            if (!missed)
            {
                region.Filled = true;
                region.FillNow = false;
            }
            // Said once, when it starts: a region that never finds a spot retries every minute.
            if (missed && !region.Retrying)
            {
                _logger.Warning(
                    "Spawn {Region} ({Name}) on {Map} found no spot for {Count} spawn(s); retrying every minute",
                    template.Id,
                    template.Name,
                    template.Map,
                    count
                );
            }

            region.Retrying = missed;

            region.NextSpawn = missed
                ? now + RetryDelay
                : now + TimeSpan.FromSeconds(_random.Next(template.MinMinutes * 60, template.MaxMinutes * 60 + 1));
        }
        catch (Exception exception)
        {
            region.NextSpawn = now + RetryDelay;
            region.Retrying = true;
            _logger.Error(exception, "Spawn {Region} failed its check", template.Id);
        }
    }

    private Dictionary<string, int> CountLive()
    {
        var live = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var mobile in _mobiles.Mobiles)
        {
            if (mobile.IsNpc && mobile.TryGetProp<string>(RegionProp, out var region))
            {
                live[region] = live.GetValueOrDefault(region) + 1;
            }
        }

        // The ids of the regions are unique, so NPCs and items share the counts.
        if (_items is not null && _regions.Any(region => region.OfItems))
        {
            _spawnedItems ??= FindSpawnedItems(_items);

            foreach (var (serial, region) in _spawnedItems.ToList())
            {
                // Decayed, deleted or taken from the ground: no longer the region's.
                if (_items.TryGet(serial, out var item) && IsOnTheGroundOf(item, region))
                {
                    live[region] = live.GetValueOrDefault(region) + 1;
                }
                else
                {
                    _spawnedItems.Remove(serial);
                }
            }
        }

        return live;
    }

    // Once, at the first check: the items a region spawned before the last stop come back with the world.
    private static Dictionary<Serial, string> FindSpawnedItems(IItemService items)
    {
        var spawned = new Dictionary<Serial, string>();

        foreach (var item in items.Items)
        {
            if (item.Props is not null &&
                item.Location == ItemLocationType.Ground &&
                item.TryGetProp<string>(RegionProp, out var region))
            {
                spawned[item.Id] = region;
            }
        }

        return spawned;
    }

    private static bool IsOnTheGroundOf(ItemEntity item, string region)
    {
        return item.Location == ItemLocationType.Ground &&
               item.TryGetProp<string>(RegionProp, out var marked) &&
               marked == region;
    }

    // UOX3 FindSpotForNPC: a random cell of the areas, out of the excluded ones, where a mobile stands under the ceiling,
    // or on the water for a mobile that swims: only there when it cannot walk, else when the cell has no land to stand on.
    private bool TryFindSpot(
        SpawnTemplate template,
        MobileMovementType movement,
        Func<int, int, bool>? taken,
        out Point3D location,
        out SpawnArea area
    )
    {
        for (var i = 0; i < SpotTries; i++)
        {
            area = template.Areas[_random.Next(0, template.Areas.Count)];
            var x = _random.Next(area.X1, area.X2 + 1);
            var y = _random.Next(area.Y1, area.Y2 + 1);

            if (template.Exclude.Any(exclude => exclude.Contains(x, y)) ||
                !_map.Contains(template.Map, x, y) ||
                taken is not null && taken(x, y))
            {
                continue;
            }

            if (!(movement != MobileMovementType.Water && TryGetLandZ(template, x, y, out var z) ||
                  movement != MobileMovementType.Land && _movement.TryGetSwimZ(template.Map, x, y, out z)) ||
                template.OnlyOutside && IsUnderRoof(template, x, y, z))
            {
                continue;
            }

            location = new(x, y, z);

            return true;
        }

        location = default;
        area = null!;

        return false;
    }

    private static bool IsTaken(MapType map, int x, int y, List<PlannedSpawn> planned)
    {
        return planned.Any(other => other.OfItems && other.Region.Map == map && other.Location.X == x && other.Location.Y == y);
    }

    // What a region spawned, of this region or of another: the decoration of the place does not count, as ModernUO
    // puts a chest where its spawner says.
    private bool HasSpawnedItem(MapType map, int x, int y)
    {
        if (_sectors is null)
        {
            return false;
        }

        var items = _sectors.GetItemsAt(map, x, y);

        for (var index = 0; index < items.Count; index++)
        {
            if (items[index].TryGetProp<string>(RegionProp, out _))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryGetLandZ(SpawnTemplate template, int x, int y, out int z)
    {
        var ceiling = template.Z ?? _movement.GetAverageZ(template.Map, x, y) + (template.PrefZ ?? DefaultPrefZ);

        return _movement.TryGetSpawnZ(template.Map, x, y, ceiling, out z) &&
               !(template.OnlyOutside && IsUnderRoof(template, x, y, z));
    }

    // UOX3's roof check, as the weather's: a static more than 10 above the spot.
    private bool IsUnderRoof(SpawnTemplate template, int x, int y, int z)
    {
        return _map.GetStatics(template.Map, x, y).Any(tile => tile.Z > z + RoofHeight);
    }

    private async Task SpawnAsync(List<PlannedSpawn> planned, CancellationToken cancellationToken)
    {
        var spawned = new List<(SpawnTemplate Template, int Count)>();

        foreach (var (template, templateId, location, area, ofItems) in planned)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                if (ofItems)
                {
                    await SpawnItemAsync(template, templateId, location, cancellationToken);

                    continue;
                }

                // The home goes into the first save, so a spawned NPC is always counted.
                var npc = await _npcs.SpawnAsync(templateId, template.Map, location, HomeOf(template, area), cancellationToken);
                var index = spawned.FindIndex(entry => entry.Template == template);

                if (index < 0)
                {
                    spawned.Add((template, 1));
                }
                else
                {
                    spawned[index] = (template, spawned[index].Count + 1);
                }

                _logger.Debug(
                    "Spawn {Region} ({Name}): {Template} {Serial} at {Location} on {Map}",
                    template.Id,
                    template.Name,
                    templateId,
                    npc.Id,
                    location,
                    template.Map
                );
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.Warning(exception, "Spawn {Region} could not spawn {Template}", template.Id, templateId);
            }
        }

        if (spawned.Count == 0 || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        try
        {
            var notice = Notice(spawned);
            await OnLoopAsync(
                () =>
                {
                    // The live NPCs are counted on the game loop, where the world changes.
                    var full = WithProgress(notice);
                    _logger.Information("{Notice}", full);
                    TellStaff(full);
                }
            );
        }
        catch (Exception exception)
        {
            _logger.Warning(exception, "The spawn notice could not be sent");
        }
    }

    private async Task SpawnItemAsync(SpawnTemplate template, string templateId, Point3D location, CancellationToken cancellationToken)
    {
        if (_itemSpawns is null)
        {
            return;
        }

        var item = await _itemSpawns.SpawnAsync(
            templateId,
            template.Map,
            location,
            new Dictionary<string, object?> { [RegionProp] = template.Id },
            cancellationToken
        );
        // The counts are read on the loop.
        await OnLoopAsync(() => (_spawnedItems ??= [])[item.Id] = template.Id);
        _logger.Debug(
            "Spawn {Region} ({Name}): item {Template} {Serial} at {Location} on {Map}",
            template.Id,
            template.Name,
            templateId,
            item.Id,
            location,
            template.Map
        );
    }

    private static Dictionary<string, object?> HomeOf(SpawnTemplate template, SpawnArea area)
    {
        return new()
        {
            [RegionProp] = template.Id,
            ["spawn.x1"] = (long)area.X1,
            ["spawn.y1"] = (long)area.Y1,
            ["spawn.x2"] = (long)area.X2,
            ["spawn.y2"] = (long)area.Y2
        };
    }

    private string Notice(List<(SpawnTemplate Template, int Count)> spawned)
    {
        if (spawned.Count == 1)
        {
            var (template, count) = spawned[0];

            return _localization.Get(CommandMessages.SpawnedInOneRegion, NameOf(template), template.Map, count);
        }

        var named = string.Join(
            ", ",
            spawned.OrderByDescending(entry => entry.Count)
                   .Take(NamedRegions)
                   .Select(entry => $"{NameOf(entry.Template)} {entry.Count}")
        );

        if (spawned.Count > NamedRegions)
        {
            named = _localization.Get(CommandMessages.SpawnedAndMore, named, spawned.Count - NamedRegions);
        }

        return _localization.Get(CommandMessages.SpawnedInRegions, spawned.Sum(entry => entry.Count), spawned.Count, named);
    }

    // How full the world is: the live NPCs of the regions against their maxes, as the gradual fill goes.
    private string WithProgress(string notice)
    {
        var live = CountLive();
        var npcs = _regions.Where(region => !region.OfItems).ToList();
        var alive = npcs.Sum(region => Math.Min(live.GetValueOrDefault(region.Template.Id), region.Template.Max));
        var max = npcs.Sum(region => region.Template.Max);
        var percent = max == 0 ? 100 : alive * 100 / max;

        return _localization.Get(CommandMessages.SpawnedWorldProgress, notice, alive, max, percent);
    }

    private static string NameOf(SpawnTemplate template)
    {
        return template.Name ?? template.Id;
    }

    private void TellStaff(string notice)
    {
        var packet = SpeechMessageHelper.CreateSystem(notice, NoticeHue);

        foreach (var session in _sessions.GetAll())
        {
            if (session.AccountType >= AccountType.GameMaster && session.CharacterId.IsValid &&
                _mobiles.IsInWorld(session.CharacterId))
            {
                SpeechMessageHelper.TrySend(_sender, session, packet);
            }
        }
    }

    private async Task OnLoopAsync(Action action)
    {
        var work = new LoopActionWorkItem(action);
        await _loop.PostAsync(work, CancellationToken.None);
        await work.Completion;
    }

    public void Dispose()
    {
        _stopping.Dispose();
    }
}
