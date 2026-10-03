using System.Runtime.CompilerServices;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Core.Utils;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Decorations;
using Moongate.Server.Ultima.Data.Moongates;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Server.Ultima.Types.Decorations;
using Moongate.Server.Ultima.Types.Items;
using Moongate.Ultima.Types;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Places the decoration files as ModernUO's <c>[Decorate</c> does, file by file: the items are built and saved off
///     the game loop in one transaction per file, which gives them their serials, then enter the world on the loop. The
///     doors of the towns, which ModernUO's <c>[DoorGen</c> reads from the map's door frames, follow the files as one more
///     file per map.
/// </summary>
public sealed class DecorationService : IDecorationService, IDisposable
{
    public const string DecorationTemplate = "decoration";
    public const string DoorTemplate = "decoration_door";
    public const string LightTemplate = "decoration_light";
    public const string TeleporterTemplate = "decoration_teleporter";
    public const string TeleporterType = "Teleporter";
    public const string KeywordTeleporterTemplate = "decoration_keyword_teleporter";
    public const string KeywordTeleporterType = "KeywordTeleporter";
    public const string PublicMoongateTemplate = "decoration_public_moongate";
    public const string PublicMoongateType = "PublicMoongate";
    public const string MoongatesFile = "moongates";
    public const string TeleportXProp = "teleport.x";
    public const string TeleportYProp = "teleport.y";
    public const string TeleportZProp = "teleport.z";
    public const string TeleportMapProp = "teleport.map";
    public const string LightProp = "light";
    public const string ProtectedProp = "protected";
    public const string TypeProp = "decoration_type";
    public const string LinkProp = "door.link";
    public const string OutsideTheMap = "outside the map";
    public const string OpenProp = "door.open";
    public const string FacingProp = "facing";
    public const string GeneratedDoorType = "DarkWoodDoor";
    public const string GeneratedDoorsFile = "generated_doors";

    // ModernUO's DarkWoodDoor: the closed graphic of a facing is this plus twice the facing.
    private const int GeneratedDoorGraphic = 0x06A5;
    private const int PublicMoongateGraphic = 0x0F6C;

    // How far apart in height two doors still stand in the same doorway.
    private const int DoorHeight = 16;

    // How far apart in height two teleporters on a cell still count as one, as ModernUO's [TelGen.
    private const int TeleporterHeight = 12;

    // ModernUO's BaseLight kinds and the light shape each gives by default.
    private static readonly Dictionary<string, LightType> LightKinds = new(StringComparer.Ordinal)
    {
        ["Brazier"] = LightType.Circle225, ["BrazierTall"] = LightType.Circle300, ["Candelabra"] = LightType.Circle225,
        ["CandelabraStand"] = LightType.Circle225, ["Candle"] = LightType.Circle150, ["CandleLarge"] = LightType.Circle150,
        ["CandleLong"] = LightType.Circle150, ["CandleShort"] = LightType.Circle150, ["CandleSkull"] = LightType.Circle150,
        ["HangingLantern"] = LightType.Circle300, ["HeatingStand"] = LightType.Circle150, ["LampPost1"] = LightType.Circle300,
        ["LampPost2"] = LightType.Circle300, ["LampPost3"] = LightType.Circle300, ["Lantern"] = LightType.Circle300,
        ["PaperLantern"] = LightType.Circle150, ["RedHangingLantern"] = LightType.Circle300,
        ["RoundPaperLantern"] = LightType.Circle150, ["ShojiLantern"] = LightType.Circle150, ["Torch"] = LightType.Circle300,
        ["WallSconce"] = LightType.WestBig, ["WallTorch"] = LightType.WestBig, ["WhiteHangingLantern"] = LightType.Circle300
    };

    private readonly ILogger _logger = Log.ForContext<DecorationService>();
    private readonly IDecorationsLoader _loader;
    private readonly IDoorGeneratorService _doors;
    private readonly IItemFactoryService _factory;
    private readonly IItemService _items;
    private readonly ISectorService _sectors;
    private readonly IWorldViewService _view;
    private readonly IGameLoopService _loop;
    private readonly IPublicMoongateService? _moongates;
    private readonly SemaphoreSlim _running = new(1, 1);

    public bool IsRunning => _running.CurrentCount == 0;

    public DecorationService(
        IDecorationsLoader loader,
        IDoorGeneratorService doors,
        IItemFactoryService factory,
        IItemService items,
        ISectorService sectors,
        IWorldViewService view,
        IGameLoopService loop,
        IPublicMoongateService? moongates = null
    )
    {
        _moongates = moongates;
        _loader = loader;
        _doors = doors;
        _factory = factory;
        _items = items;
        _sectors = sectors;
        _view = view;
        _loop = loop;
    }

    public async Task<DecorationResult> DecorateAsync(
        IProgress<DecorationFileResult>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        // Two runs would both find the spots free before either adds its items.
        if (!_running.Wait(0, CancellationToken.None))
        {
            throw new InvalidOperationException("A decoration is already running.");
        }

        try
        {
            return await DecorateFilesAsync(progress, cancellationToken);
        }
        finally
        {
            _running.Release();
        }
    }

    private async Task<DecorationResult> DecorateFilesAsync(
        IProgress<DecorationFileResult>? progress,
        CancellationToken cancellationToken
    )
    {
        var files = await _loader.LoadAsync(cancellationToken);
        var placed = 0;
        var present = 0;
        var skipped = 0;
        var count = 0;

        // The generated doors and the public moongates come last: an item of a file on the same spot wins.
        await foreach (var file in WithGeneratedFilesAsync(files, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await DecorateFileAsync(file, cancellationToken);
            count++;
            placed += result.Placed;
            present += result.Present;
            skipped += result.Skipped;

            _logger.Information(
                "Decorating {Folder}/{Name}: {Placed} placed, {Present} already there, {Skipped} skipped {SkippedByType}",
                result.Folder,
                result.Name,
                result.Placed,
                result.Present,
                result.Skipped,
                result.SkippedByType
            );
            progress?.Report(result);
        }

        _logger.Information(
            "Decoration done: {Placed} placed, {Present} already there, {Skipped} skipped in {Files} files",
            placed,
            present,
            skipped,
            count
        );

        return new(placed, present, skipped, count);
    }

    private async IAsyncEnumerable<DecorationFile> WithGeneratedFilesAsync(
        IReadOnlyList<DecorationFile> files,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        foreach (var file in files)
        {
            yield return file;
        }

        foreach (var map in Enum.GetValues<MapType>())
        {
            if (await GenerateDoorsAsync(map, cancellationToken) is { } doors)
            {
                yield return doors;
            }
        }

        IReadOnlyList<MoongateFacet> facets = [];
        await OnLoopAsync(() => facets = _moongates?.GetFacets() ?? []);

        foreach (var facet in facets)
        {
            yield return MoongatesOf(facet);
        }
    }

    // The gates of a map's public moongates, as a decoration file: one on each destination, as ModernUO's [MoonGen.
    private static DecorationFile MoongatesOf(MoongateFacet facet)
    {
        return new()
        {
            Folder = EnumNameUtils.Format(facet.Map),
            Name = MoongatesFile,
            Maps = [facet.Map],
            Blocks = facet.Destination
                          .Select(
                              destination => new DecorationBlock
                              {
                                  Type = PublicMoongateType,
                                  ItemId = PublicMoongateGraphic,
                                  Props = destination.Hue == 0
                                      ? new Dictionary<string, object>(StringComparer.Ordinal)
                                      : new Dictionary<string, object>(StringComparer.Ordinal) { ["hue"] = (long)destination.Hue },
                                  Locations = [destination.Location]
                              }
                          )
                          .ToList()
        };
    }

    // The doors the door frames of a map call for, as a decoration file; null for a map that is not scanned. The map is
    // read on the loop, a chunk per work item.
    private async Task<DecorationFile?> GenerateDoorsAsync(MapType map, CancellationToken cancellationToken)
    {
        IReadOnlyList<Rectangle2D> chunks = [];
        await OnLoopAsync(() => chunks = _doors.ChunksOf(map));

        if (chunks.Count == 0)
        {
            return null;
        }

        var doors = new List<GeneratedDoor>();

        foreach (var chunk in chunks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await OnLoopAsync(() => doors.AddRange(_doors.Scan(map, chunk)));
        }

        // ModernUO's regions overlap: a frame in both is found twice.
        var blocks = doors.Distinct()
                          .GroupBy(door => door.Facing)
                          .OrderBy(group => group.Key)
                          .Select(
                              group => new DecorationBlock
                              {
                                  Type = GeneratedDoorType,
                                  ItemId = GeneratedDoorGraphic + 2 * (int)group.Key,
                                  Props = new Dictionary<string, object>(StringComparer.Ordinal)
                                  {
                                      [FacingProp] = EnumNameUtils.Format(group.Key)
                                  },
                                  Locations = group.Select(door => door.Location).ToList()
                              }
                          )
                          .ToList();

        return new()
        {
            Folder = EnumNameUtils.Format(map),
            Name = GeneratedDoorsFile,
            Maps = [map],
            Blocks = blocks
        };
    }

    private async Task<DecorationFileResult> DecorateFileAsync(DecorationFile file, CancellationToken cancellationToken)
    {
        var skipped = new Dictionary<string, int>(StringComparer.Ordinal);
        var candidates = new List<(DecorationBlock Block, MapType Map, Point3D Location)>();

        foreach (var block in file.Blocks)
        {
            if (block.ItemId is null || IsSkipped(block.Type) || HasUnknownMap(block))
            {
                Count(skipped, block.Type, block.Locations.Count * file.Maps.Count);

                continue;
            }

            foreach (var map in file.Maps)
            {
                candidates.AddRange(block.Locations.Select(location => (block, map, location)));
            }
        }

        // What is on the ground is read on the loop; the new items are built and saved off it.
        var free = new List<(DecorationBlock Block, MapType Map, Point3D Location)>();
        var present = 0;
        await OnLoopAsync(
            () =>
            {
                var seen = new HashSet<(MapType, Point3D, int)>();
                // The teleporters this file already keeps: the ground does not show them yet.
                var teleporters = new List<(string Type, MapType Map, Point3D Location)>();

                foreach (var candidate in candidates)
                {
                    var (block, map, location) = candidate;

                    if (!_sectors.IsInside(map, location.X, location.Y))
                    {
                        Count(skipped, OutsideTheMap, 1);
                    }
                    else if (!seen.Add((map, location, block.ItemId!.Value)) ||
                             IsThere(map, location, block) ||
                             IsTeleporter(block.Type) &&
                             teleporters.Any(
                                 other => other.Type == block.Type && other.Map == map && SharesSpot(other.Location, location)
                             ))
                    {
                        present++;
                    }
                    else
                    {
                        free.Add(candidate);

                        if (IsTeleporter(block.Type))
                        {
                            teleporters.Add((block.Type, map, location));
                        }
                    }
                }
            }
        );

        var items = free.Select(candidate => Build(candidate.Block, candidate.Map, candidate.Location)).ToList();

        if (items.Count > 0)
        {
            await _factory.SaveAsync(items, cancellationToken);

            // Saved: from here the items enter the world whatever happens, or a second run would save them again. The
            // links need the serials the save gave; if saving them fails, the next world save writes them.
            var linked = LinkDoors(items);

            if (linked.Count > 0)
            {
                try
                {
                    await _factory.SaveAsync(linked, CancellationToken.None);
                }
                catch (Exception exception)
                {
                    _logger.Warning(exception, "Saving the door links of {Folder}/{Name} failed", file.Folder, file.Name);
                }
            }

            await OnLoopAsync(
                () =>
                {
                    _items.Add(items);

                    foreach (var item in items)
                    {
                        _view.ItemAppeared(item);
                    }
                }
            );
        }

        return new(file.Folder, file.Name, items.Count, present, skipped);
    }

    // Kinds whose behaviour is not written yet: a plain item in their place would look or act wrong. The plain
    // teleporter (teleporter.lua), the one that answers a word (keyword_teleport.lua) and the public moongate
    // (public_moongate.lua) are written; the teleporters that ask for a skill or a quest are not.
    private static bool IsSkipped(string type)
    {
        return type is "Spawner" or "MarkContainer" ||
               !IsTeleporter(type) && type.EndsWith("Teleporter", StringComparison.Ordinal) ||
               type.EndsWith("Addon", StringComparison.Ordinal);
    }

    // A teleporter to a map that does not exist would send its players to that spot of its own map.
    private static bool HasUnknownMap(DecorationBlock block)
    {
        return IsTeleporter(block.Type) &&
               block.Props.GetValueOrDefault("map_dest") is { } name &&
               !(name is string text && EnumNameUtils.TryParse<MapType>(text, out _));
    }

    private static bool IsTeleporter(string type)
    {
        return type is TeleporterType or KeywordTeleporterType;
    }

    private static string TeleporterTemplateOf(string type)
    {
        return type == KeywordTeleporterType ? KeywordTeleporterTemplate : TeleporterTemplate;
    }

    private static bool IsDoor(string type)
    {
        return type.Contains("Door", StringComparison.Ordinal) || type.Contains("Gate", StringComparison.Ordinal);
    }

    // The same graphic on the spot, or a door opened from it: one graphic further and up to a tile aside, its closed
    // spot kept by door.lua.
    // A light lit or doused since is still the same kind on the same spot.
    // A doorway holds one door: another kind already in it, closed or opened from it, keeps a new one away.
    // A cell holds one teleporter within reach of a mobile's step, as ModernUO's [TelGen keeps one.
    private bool IsThere(MapType map, Point3D location, DecorationBlock block)
    {
        var graphic = block.ItemId!.Value;
        var isLight = LightKinds.ContainsKey(block.Type);
        var isDoor = IsDoor(block.Type);
        var isTeleporter = IsTeleporter(block.Type);
        var teleporterTemplate = TeleporterTemplateOf(block.Type);

        return _sectors.GetItemsInRange(map, location, 1)
                       .Any(item => item.ItemId == graphic && item.GroundLocation == location ||
                                    isDoor && item.TemplateId == DoorTemplate && StandsInDoorway(item, location) ||
                                    isTeleporter && item.TemplateId == teleporterTemplate && SharesSpot(item, location) ||
                                    item.ItemId == graphic + 1 && IsOpenFrom(item, location) ||
                                    isLight && item.GroundLocation == location && item.TemplateId == LightTemplate &&
                                    Equals(item.Props?.GetValueOrDefault(TypeProp), block.Type));
    }

    private static bool SharesSpot(ItemEntity teleporter, Point3D location)
    {
        return teleporter.GroundLocation is { } spot && SharesSpot(spot, location);
    }

    private static bool SharesSpot(Point3D spot, Point3D location)
    {
        return spot.X == location.X && spot.Y == location.Y && Math.Abs(spot.Z - location.Z) <= TeleporterHeight;
    }

    private static bool StandsInDoorway(ItemEntity door, Point3D location)
    {
        var closed = door.Props is { } props && props.GetValueOrDefault(OpenProp) is true
            ? props.GetValueOrDefault("door.x") is long x &&
              props.GetValueOrDefault("door.y") is long y &&
              props.GetValueOrDefault("door.z") is long z
                ? new Point3D((int)x, (int)y, (int)z)
                : (Point3D?)null
            : door.GroundLocation;

        return closed is { } spot &&
               spot.X == location.X &&
               spot.Y == location.Y &&
               Math.Abs(spot.Z - location.Z) < DoorHeight;
    }

    private static bool IsOpenFrom(ItemEntity item, Point3D location)
    {
        return item.Props is { } props &&
               props.GetValueOrDefault(OpenProp) is true &&
               props.GetValueOrDefault("door.x") is long x && x == location.X &&
               props.GetValueOrDefault("door.y") is long y && y == location.Y &&
               props.GetValueOrDefault("door.z") is long z && z == location.Z;
    }

    private ItemEntity Build(DecorationBlock block, MapType map, Point3D location)
    {
        var door = IsDoor(block.Type);
        var isLight = LightKinds.TryGetValue(block.Type, out var defaultLight);
        var teleporter = IsTeleporter(block.Type);
        var item = _factory.Create(
            door ? DoorTemplate :
            isLight ? LightTemplate :
            teleporter ? TeleporterTemplateOf(block.Type) :
            block.Type == PublicMoongateType ? PublicMoongateTemplate : DecorationTemplate
        );
        var props = new Dictionary<string, object?>(StringComparer.Ordinal);
        item.ItemId = block.ItemId!.Value;

        foreach (var (key, value) in block.Props)
        {
            switch (key, value)
            {
                case ("hue", long hue and >= 0 and <= ushort.MaxValue):
                    item.Hue = new Hue((ushort)hue);

                    break;
                case ("name", string name):
                    item.Name = name;

                    break;
                // What the teleporter scripts read: the destination, and its map as a MapType number.
                case ("point_dest", Point3D destination) when teleporter:
                    props[TeleportXProp] = (long)destination.X;
                    props[TeleportYProp] = (long)destination.Y;
                    props[TeleportZProp] = (long)destination.Z;

                    break;
                case ("map_dest", string mapName) when teleporter && EnumNameUtils.TryParse<MapType>(mapName, out var mapDest):
                    props[TeleportMapProp] = (long)mapDest;

                    break;
                // An item's props keep no points.
                case (_, Point3D):
                    break;
                default:
                    props[key] = value;

                    break;
            }
        }

        if (door || isLight)
        {
            props[TypeProp] = block.Type;
        }

        if (isLight)
        {
            AddLightProps(props, defaultLight);
        }

        // As ModernUO's PublicMoongate: the gate glows, unless the data gives it another light.
        if (block.Type == PublicMoongateType)
        {
            props.TryAdd(LightProp, EnumNameUtils.Format(LightType.Circle300));
        }

        item.Props = props.Count > 0 ? props : null;
        item.PlaceOnGround(map, location);

        return item;
    }

    // As ModernUO's decorate: the graphic already says lit or unlit; the shape is the data's or the kind's, and a light is
    // protected (only staff light or douse it) unless the data says unprotected.
    private static void AddLightProps(Dictionary<string, object?> props, LightType defaultLight)
    {
        var light = props.GetValueOrDefault(LightProp) is string name && EnumNameUtils.TryParse<LightType>(name, out var parsed)
            ? parsed
            : defaultLight;
        props[LightProp] = EnumNameUtils.Format(light);
        props[ProtectedProp] = props.GetValueOrDefault("unprotected") is not true;
        props.Remove("unprotected");
        props.Remove("unlit");
    }

    // Pairs each door with an unpaired door of the same kind next to it, as ModernUO's double doors: its halves hang on
    // opposite sides, so two doors with the same facing are two doorways side by side.
    private static List<ItemEntity> LinkDoors(List<ItemEntity> items)
    {
        var doors = items.Where(item => item.TemplateId == DoorTemplate).ToList();
        var linked = new List<ItemEntity>();

        foreach (var door in doors.Where(door => !door.Props!.ContainsKey(LinkProp)))
        {
            var partner = doors.FirstOrDefault(
                other => other != door && !other.Props!.ContainsKey(LinkProp) && Adjacent(door, other)
            );

            if (partner is null)
            {
                continue;
            }

            door.Props![LinkProp] = (long)partner.Id.Value;
            partner.Props![LinkProp] = (long)door.Id.Value;
            linked.Add(door);
            linked.Add(partner);
        }

        return linked;
    }

    private static bool Adjacent(ItemEntity door, ItemEntity other)
    {
        var dx = Math.Abs(door.X!.Value - other.X!.Value);
        var dy = Math.Abs(door.Y!.Value - other.Y!.Value);

        return door.Map == other.Map &&
               door.Z == other.Z &&
               dx + dy == 1 &&
               Equals(door.Props![TypeProp], other.Props![TypeProp]) &&
               !(door.Props.GetValueOrDefault(FacingProp) is { } facing &&
                 Equals(facing, other.Props.GetValueOrDefault(FacingProp)));
    }

    private static void Count(Dictionary<string, int> skipped, string type, int count)
    {
        skipped[type] = skipped.GetValueOrDefault(type) + count;
    }

    private async Task OnLoopAsync(Action action)
    {
        var work = new LoopActionWorkItem(action);
        await _loop.PostAsync(work, CancellationToken.None);
        await work.Completion;
    }

    public void Dispose()
    {
        _running.Dispose();
    }
}
