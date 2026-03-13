using System.Diagnostics;
using Humanizer;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Data.Internal.Scripting;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.EvenLoop;
using Moongate.Server.Interfaces.Services.World;
using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Maps;

namespace Moongate.Server.Commands.WorldGen;

[RegisterConsoleCommand(
    "create_spawners",
    "Create spawner items from loaded spawns data. Usage: .create_spawners [mapId]",
    CommandSourceType.Console | CommandSourceType.InGame
)]
public sealed class CreateSpawnersCommand : ICommandExecutor
{
    private const string SpawnerTemplateId = "spawn";

    private readonly IEntityFactoryService _entityFactoryService;
    private readonly ISeedDataService _seedDataService;
    private readonly IItemService _itemService;
    private readonly IBackgroundJobService _backgroundJobService;

    public CreateSpawnersCommand(
        IEntityFactoryService entityFactoryService,
        ISeedDataService seedDataService,
        IItemService itemService,
        IBackgroundJobService backgroundJobService
    )
    {
        _entityFactoryService = entityFactoryService;
        _seedDataService = seedDataService;
        _itemService = itemService;
        _backgroundJobService = backgroundJobService;
    }

    public Task ExecuteCommandAsync(CommandSystemContext context)
    {
        var mapIds = Map.MapIDs;

        if (context.Arguments.Length > 0 && int.TryParse(context.Arguments[0], out var mapId))
        {
            mapIds = [mapId];
        }

        context.Print("Starting spawner item generation...");

        foreach (var currentMapId in mapIds)
        {
            _backgroundJobService.EnqueueBackground(() => CreateForMapAsync(currentMapId, context));
        }

        return Task.CompletedTask;
    }

    private async Task CreateForMapAsync(int mapId, CommandSystemContext context)
    {
        var startTimestamp = Stopwatch.GetTimestamp();
        var spawns = _seedDataService.GetSpawnsByMap(mapId);
        var created = 0;

        foreach (var spawn in spawns)
        {
            if (spawn.Guid == Guid.Empty)
            {
                context.PrintWarning("Skipping spawn '{0}' on map {1}: GUID is empty.", spawn.Name, spawn.MapId);

                continue;
            }

            var item = _entityFactoryService.CreateItemFromTemplate(SpawnerTemplateId);
            item.MapId = spawn.MapId;
            item.Location = spawn.Location;
            item.Name = spawn.Name;
            item.SetCustomString(ItemCustomParamKeys.Spawner.SpawnerId, spawn.Guid.ToString("D"));

            await _itemService.CreateItemAsync(item);
            created++;
        }

        context.Print(
            $"Spawner item generation for map {mapId} complete: created {created} items in {Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds.Milliseconds().Humanize()}."
        );
    }
}
