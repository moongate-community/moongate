using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Core.Utils;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Decorations;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Server.Ultima.Types.Items;
using Moongate.Ultima.Types;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Places the decoration files as ModernUO's <c>[Decorate</c> does, file by file: the items are built and saved off
///     the game loop in one transaction per file, which gives them their serials, then enter the world on the loop.
/// </summary>
public sealed class DecorationService : IDecorationService, IDisposable
{
    public const string DecorationTemplate = "decoration";
    public const string DoorTemplate = "decoration_door";
    public const string LightTemplate = "decoration_light";
    public const string LightProp = "light";
    public const string ProtectedProp = "protected";
    public const string TypeProp = "decoration_type";
    public const string LinkProp = "door.link";
    public const string OutsideTheMap = "outside the map";
    public const string OpenProp = "door.open";

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
    private readonly IItemFactoryService _factory;
    private readonly IItemService _items;
    private readonly ISectorService _sectors;
    private readonly IWorldViewService _view;
    private readonly IGameLoopService _loop;
    private readonly SemaphoreSlim _running = new(1, 1);

    public bool IsRunning => _running.CurrentCount == 0;

    public DecorationService(
        IDecorationsLoader loader,
        IItemFactoryService factory,
        IItemService items,
        ISectorService sectors,
        IWorldViewService view,
        IGameLoopService loop
    )
    {
        _loader = loader;
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

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await DecorateFileAsync(file, cancellationToken);
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
            files.Count
        );

        return new(placed, present, skipped, files.Count);
    }

    private async Task<DecorationFileResult> DecorateFileAsync(DecorationFile file, CancellationToken cancellationToken)
    {
        var skipped = new Dictionary<string, int>(StringComparer.Ordinal);
        var candidates = new List<(DecorationBlock Block, MapType Map, Point3D Location)>();

        foreach (var block in file.Blocks)
        {
            if (block.ItemId is null || IsSkipped(block.Type))
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

                foreach (var candidate in candidates)
                {
                    var (block, map, location) = candidate;

                    if (!_sectors.IsInside(map, location.X, location.Y))
                    {
                        Count(skipped, OutsideTheMap, 1);
                    }
                    else if (!seen.Add((map, location, block.ItemId!.Value)) || IsThere(map, location, block))
                    {
                        present++;
                    }
                    else
                    {
                        free.Add(candidate);
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

    // Kinds whose behaviour is not written yet: a plain item in their place would look or act wrong.
    private static bool IsSkipped(string type)
    {
        return type is "Spawner" or "MarkContainer" or "PublicMoongate" ||
               type.EndsWith("Teleporter", StringComparison.Ordinal) ||
               type.EndsWith("Addon", StringComparison.Ordinal);
    }

    private static bool IsDoor(string type)
    {
        return type.Contains("Door", StringComparison.Ordinal) || type.Contains("Gate", StringComparison.Ordinal);
    }

    // The same graphic on the spot, or a door opened from it: one graphic further and up to a tile aside, its closed
    // spot kept by door.lua.
    // A light lit or doused since is still the same kind on the same spot.
    private bool IsThere(MapType map, Point3D location, DecorationBlock block)
    {
        var graphic = block.ItemId!.Value;
        var isLight = LightKinds.ContainsKey(block.Type);

        return _sectors.GetItemsInRange(map, location, 1)
                       .Any(item => item.ItemId == graphic && item.GroundLocation == location ||
                                    item.ItemId == graphic + 1 && IsOpenFrom(item, location) ||
                                    isLight && item.GroundLocation == location && item.TemplateId == LightTemplate &&
                                    Equals(item.Props?.GetValueOrDefault(TypeProp), block.Type));
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
        var item = _factory.Create(door ? DoorTemplate : isLight ? LightTemplate : DecorationTemplate);
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

    // Pairs each door with an unpaired door of the same kind next to it, as ModernUO's double doors.
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
               Equals(door.Props![TypeProp], other.Props![TypeProp]);
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
