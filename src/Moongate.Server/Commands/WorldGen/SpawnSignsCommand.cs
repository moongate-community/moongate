using System.Diagnostics;
using Humanizer;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Data.World;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.EvenLoop;
using Moongate.Server.Interfaces.Services.World;
using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Commands.WorldGen;

[RegisterConsoleCommand(
    "spawn_signs",
    "Run signs generation immediately. Usage: .spawn_signs",
    CommandSourceType.Console | CommandSourceType.InGame,
    AccountType.Administrator
)]
public class SpawnSignsCommand : ICommandExecutor
{
    private readonly string _signIdTemplate = "sign";

    private readonly IEntityFactoryService _entityFactoryService;
    private readonly ISeedDataService _seedDataService;
    private readonly IItemService _itemService;

    private readonly IBackgroundJobService _backgroundJobService;

    public SpawnSignsCommand(
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

    public async Task ExecuteCommandAsync(CommandSystemContext context)
    {
        var startTime = Stopwatch.GetTimestamp();
        context.Print("Starting sign generation...");
        await Task.Delay(1000);

        foreach (var mapId in Map.MapIDs)
        {
            _backgroundJobService.EnqueueBackground(
                () => CreateSignForMapIdAsync(_seedDataService.GetSignsByMap(mapId), mapId, context)
            );
        }
    }

    private async Task CreateSignForMapIdAsync(IReadOnlyList<SignEntry> signs, int mapId, CommandSystemContext context)
    {
        var startTime = Stopwatch.GetTimestamp();

        foreach (var sign in signs)
        {
            var signEntity = _entityFactoryService.CreateItemFromTemplate(_signIdTemplate);

            signEntity.ItemId = sign.ItemId.ToInt32();
            signEntity.Location = sign.Location;
            signEntity.MapId = sign.MapId;

            if (sign.Text.StartsWith('#'))
            {
                signEntity.SetCustomString("label_number", sign.Text[1..]);
            }
            else
            {
                signEntity.Name = sign.Text;
            }

            await _itemService.CreateItemAsync(signEntity);
        }

        context.Print(
            $"Signs generation for mapId: {mapId}  (total signs: {signs.Count}) complete in {Stopwatch.GetElapsedTime(startTime).TotalMilliseconds.Milliseconds().Humanize()}"
        );
    }
}
