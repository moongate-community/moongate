using System.Diagnostics;
using Humanizer;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.EvenLoop;
using Moongate.Server.Interfaces.Services.World;
using Moongate.Server.Services.World;
using Moongate.Server.Types.Commands;
using Moongate.Server.Types.World;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Templates.Items;

namespace Moongate.Server.Commands.WorldGen;

[RegisterConsoleCommand(
    "spawn_decorations",
    "Run door world generation immediately. Usage: .spawn_doors",
    CommandSourceType.Console | CommandSourceType.InGame
)]
public class SpawnDecorationsCommand : ICommandExecutor
{
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

    private async Task DecorateMapAsync(int mapId, CommandSystemContext context)
    {
        var decorations = _seedDataService.GetDecorationsByMap(mapId);

        var startTime = Stopwatch.GetTimestamp();
        var spawnedCount = 0;

        foreach (var decoration in decorations)
        {
            if (_itemFactoryService.TryGetItemTemplate(decoration.TypeName, out _))
            {
                var item = _itemFactoryService.CreateItemFromTemplate(decoration.TypeName);

                item.MapId = mapId;
                item.Location = decoration.Location;

                if (decoration.Parameters.Count > 0)
                {
                    if (decoration.Parameters.TryGetValue("Hue", out var hueValue))
                    {
                        var hue = HueSpec.ParseFromString(hueValue);
                        item.Hue = hue.Resolve();
                    }

                    if (decoration.Parameters.TryGetValue("Facing", out var facingValue) &&
                        Enum.TryParse<DoorGenerationFacing>(facingValue, true, out var facing))
                    {
                        item.Direction = facing.ToDirectionType();
                        item.ItemId = facing.ToItemId(item.ItemId);
                    }

                    if (decoration.Parameters.TryGetValue("Name", out var nameValue))
                    {
                        item.Name = nameValue;
                    }

                    await _itemService.CreateItemAsync(item);
                    spawnedCount++;
                }
            }
        }

        context.Print(
            $"Finished spawning decorations for map {mapId}. Spawned {spawnedCount} decorations in {Stopwatch.GetElapsedTime(startTime).TotalMilliseconds.Milliseconds().Humanize()} seconds."
        );
    }
}
