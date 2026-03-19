using System.Diagnostics;
using System.Globalization;
using Humanizer;
using Moongate.Core.Extensions.Strings;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Data.Internal.Scripting;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.EvenLoop;
using Moongate.Server.Interfaces.Services.World;
using Moongate.Server.Services.World;
using Moongate.Server.Types.Commands;
using Moongate.Server.Types.World;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Templates.Items;

namespace Moongate.Server.Commands.WorldGen;

[RegisterConsoleCommand(
    "spawn_decorations",
    "Run door world generation immediately. Usage: .spawn_doors",
    CommandSourceType.Console | CommandSourceType.InGame
)]
public class SpawnDecorationsCommand : ICommandExecutor
{
    private static readonly Dictionary<string, string> CustomParameterAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["PointDest"] = "point_dest",
        ["SoundID"] = "sound_id",
        ["SourceEffect"] = "source_effect",
        ["DestEffect"] = "dest_effect"
    };

    private readonly ISeedDataService _seedDataService;
    private readonly IBackgroundJobService _backgroundJobService;

    private readonly IItemService _itemService;
    private readonly IItemFactoryService _itemFactoryService;

    public SpawnDecorationsCommand(
        ISeedDataService seedDataService,
        IBackgroundJobService backgroundJobService,
        IItemFactoryService itemFactoryService,
        IItemService itemService
    )
    {
        _seedDataService = seedDataService;
        _backgroundJobService = backgroundJobService;
        _itemFactoryService = itemFactoryService;
        _itemService = itemService;
    }

    public Task ExecuteCommandAsync(CommandSystemContext context)
    {
        try
        {
            var allMapIds = Map.MapIDs;

            if (context.Arguments.Length > 0)
            {
                var mapIdArg = context.Arguments[0];
                allMapIds = [int.Parse(mapIdArg)];
            }

            foreach (var mapId in allMapIds)
            {
                _backgroundJobService.EnqueueBackground(() => DecorateMapAsync(mapId, context));
            }

            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    private static void ApplyDecorationParameters(UOItemEntity item, IReadOnlyDictionary<string, string> parameters)
    {
        if (parameters.Count == 0)
        {
            return;
        }

        if (parameters.TryGetValue("Hue", out var hueValue))
        {
            var hue = HueSpec.ParseFromString(hueValue);
            item.Hue = hue.Resolve();
        }

        if (parameters.TryGetValue("Facing", out var facingValue) &&
            Enum.TryParse<DoorGenerationFacing>(facingValue, true, out var facing))
        {
            item.Direction = facing.ToDirectionType();
            item.ItemId = facing.ToItemId(item.ItemId);
            item.SetCustomString(ItemCustomParamKeys.Door.Facing, facing.ToString());
        }

        if (parameters.TryGetValue("Name", out var nameValue))
        {
            item.Name = nameValue;
        }

        foreach (var (rawKey, rawValue) in parameters)
        {
            if (string.Equals(rawKey, "Hue", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(rawKey, "Facing", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(rawKey, "Name", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var key = NormalizeCustomKey(rawKey);
            var value = rawValue.Trim();

            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (string.Equals(rawKey, "PointDest", StringComparison.OrdinalIgnoreCase) &&
                Point3D.TryParse(value, null, out var pointDest))
            {
                item.SetCustomLocation(key, pointDest);

                continue;
            }

            if (string.Equals(rawKey, "Delay", StringComparison.OrdinalIgnoreCase) &&
                TryParseDelayMilliseconds(value, out var delayMilliseconds))
            {
                item.SetCustomInteger("delay_ms", delayMilliseconds);

                continue;
            }

            if (bool.TryParse(value, out var boolValue))
            {
                item.SetCustomBoolean(key, boolValue);

                continue;
            }

            if (TryParseInteger(value, out var integerValue))
            {
                item.SetCustomInteger(key, integerValue);

                continue;
            }

            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue))
            {
                item.SetCustomDouble(key, doubleValue);

                continue;
            }

            item.SetCustomString(key, value);
        }
    }

    private async Task DecorateMapAsync(int mapId, CommandSystemContext context)
    {
        var decorations = _seedDataService.GetDecorationsByMap(mapId);

        var startTime = Stopwatch.GetTimestamp();
        var spawnedCount = 0;

        foreach (var decoration in decorations)
        {
            var found = _itemFactoryService.TryGetItemTemplate(decoration.TypeName, out _);

            UOItemEntity item = null;

            if (found)
            {
                item = _itemFactoryService.CreateItemFromTemplate(decoration.TypeName);
            }
            else
            {
                item = _itemFactoryService.CreateItemFromTemplate("static");
            }

            if (decoration.ItemId != Serial.Zero)
            {
                item.ItemId = (int)decoration.ItemId.Value;
            }

            item.MapId = mapId;
            item.Location = decoration.Location;

            ApplyDecorationParameters(item, decoration.Parameters);

            await _itemService.CreateItemAsync(item);
            spawnedCount++;
        }

        context.Print(
            $"Finished spawning decorations for map {mapId}. Spawned {spawnedCount} decorations in {Stopwatch.GetElapsedTime(startTime).TotalMilliseconds.Milliseconds().Humanize()} seconds."
        );
    }

    private static string NormalizeCustomKey(string rawKey)
    {
        if (CustomParameterAliases.TryGetValue(rawKey, out var alias))
        {
            return alias;
        }

        return rawKey.ToSnakeCase();
    }

    private static bool TryParseDelayMilliseconds(string value, out long milliseconds)
    {
        milliseconds = 0;

        if (!TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var delay))
        {
            return false;
        }

        milliseconds = (long)delay.TotalMilliseconds;

        return true;
    }

    private static bool TryParseInteger(string value, out long parsed)
    {
        parsed = 0;

        if (Serial.TryParse(value, CultureInfo.InvariantCulture, out var serial))
        {
            parsed = (uint)serial;

            return true;
        }

        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed);
    }
}
