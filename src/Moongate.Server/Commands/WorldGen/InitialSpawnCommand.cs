using System.Diagnostics;
using Humanizer;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Interfaces.Services.EvenLoop;
using Moongate.Server.Interfaces.Services.Persistence;
using Moongate.Server.Interfaces.Services.World;
using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Commands.WorldGen;

[RegisterConsoleCommand(
    "initial_spawn",
    "Force a spawn attempt for all persisted spawner items. Usage: .initial_spawn [mapId]",
    CommandSourceType.Console | CommandSourceType.InGame
)]
public sealed class InitialSpawnCommand : ICommandExecutor
{
    private const int ProgressInterval = 500;

    private readonly IPersistenceService _persistenceService;
    private readonly ISpawnService _spawnService;
    private readonly IBackgroundJobService _backgroundJobService;

    public InitialSpawnCommand(
        IPersistenceService persistenceService,
        ISpawnService spawnService,
        IBackgroundJobService backgroundJobService
    )
    {
        _persistenceService = persistenceService;
        _spawnService = spawnService;
        _backgroundJobService = backgroundJobService;
    }

    public Task ExecuteCommandAsync(CommandSystemContext context)
    {
        int? mapId = null;

        if (context.Arguments.Length > 0)
        {
            if (!int.TryParse(context.Arguments[0], out var parsedMapId))
            {
                context.PrintWarning("Usage: .initial_spawn [mapId]");

                return Task.CompletedTask;
            }

            mapId = parsedMapId;
        }

        context.Print("Starting initial spawn...");
        _backgroundJobService.EnqueueBackground(() => ExecuteInitialSpawnAsync(context, mapId));

        return Task.CompletedTask;
    }

    private async Task ExecuteInitialSpawnAsync(CommandSystemContext context, int? mapId)
    {
        var startTimestamp = Stopwatch.GetTimestamp();
        var spawners = await _persistenceService.UnitOfWork.Items.QueryAsync(
                           item =>
                               item.ParentContainerId == Serial.Zero &&
                               item.EquippedMobileId == Serial.Zero &&
                               (!mapId.HasValue || item.MapId == mapId.Value) &&
                               item.TryGetCustomString("spawner_id", out var spawnerId) &&
                               !string.IsNullOrWhiteSpace(spawnerId),
                           static item => item
                       );

        var processed = 0;
        var triggered = 0;
        var failedOrSkipped = 0;
        var total = spawners.Count;

        foreach (var spawner in spawners)
        {
            var result = await _spawnService.TriggerAsync(spawner);
            processed++;

            if (result)
            {
                triggered++;
            }
            else
            {
                failedOrSkipped++;
            }

            if (processed % ProgressInterval == 0)
            {
                context.Print(
                    "Initial spawn progress: processed {0}/{1}, triggered {2}, skipped/failed {3}",
                    processed,
                    total,
                    triggered,
                    failedOrSkipped
                );
            }
        }

        context.Print(
            "Initial spawn complete: processed {0} spawners, triggered {1}, skipped/failed {2} in {3}.",
            processed,
            triggered,
            failedOrSkipped,
            Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds.Milliseconds().Humanize()
        );
    }
}
