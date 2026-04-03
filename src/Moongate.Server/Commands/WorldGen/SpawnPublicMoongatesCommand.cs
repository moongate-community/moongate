using Moongate.Server.Attributes;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Data.Internal.Scripting;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.EvenLoop;
using Moongate.Server.Interfaces.Services.Persistence;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Interfaces.Services.World;
using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Commands.WorldGen;

/// <summary>
/// Rebuilds the shard-wide public moongate network from the shared Lua dataset.
/// </summary>
[RegisterConsoleCommand(
    "spawn_public_moongates",
    "Regenerate the shared public moongate network from Lua data. Usage: .spawn_public_moongates",
    CommandSourceType.Console | CommandSourceType.InGame,
    AccountType.GameMaster
)]
public sealed class SpawnPublicMoongatesCommand : ICommandExecutor
{
    private const string PublicMoongateTemplateId = "public_moongate";

    private readonly IPersistenceService _persistenceService;
    private readonly IBackgroundJobService _backgroundJobService;
    private readonly IPublicMoongateDefinitionService _publicMoongateDefinitionService;
    private readonly IItemFactoryService _itemFactoryService;
    private readonly IItemService _itemService;
    private readonly ISpatialWorldService _spatialWorldService;

    public SpawnPublicMoongatesCommand(
        IPersistenceService persistenceService,
        IBackgroundJobService backgroundJobService,
        IPublicMoongateDefinitionService publicMoongateDefinitionService,
        IItemFactoryService itemFactoryService,
        IItemService itemService,
        ISpatialWorldService spatialWorldService
    )
    {
        _persistenceService = persistenceService;
        _backgroundJobService = backgroundJobService;
        _publicMoongateDefinitionService = publicMoongateDefinitionService;
        _itemFactoryService = itemFactoryService;
        _itemService = itemService;
        _spatialWorldService = spatialWorldService;
    }

    public Task ExecuteCommandAsync(CommandSystemContext context)
    {
        if (context.Arguments.Length > 0)
        {
            context.Print("Usage: .spawn_public_moongates");

            return Task.CompletedTask;
        }

        if (!_itemFactoryService.TryGetItemTemplate(PublicMoongateTemplateId, out _))
        {
            context.PrintError("Public moongate template '{0}' was not found.", PublicMoongateTemplateId);

            return Task.CompletedTask;
        }

        context.Print("Rebuilding public moongate network...");
        _backgroundJobService.EnqueueBackground(() => ExecuteSpawnAsync(context));

        return Task.CompletedTask;
    }

    private async Task ExecuteSpawnAsync(CommandSystemContext context)
    {
        try
        {
            var groups = _publicMoongateDefinitionService.Load();

            if (groups.Count == 0)
            {
                context.PrintWarning("No public moongate destinations are configured.");

                return;
            }

            var existingGateIds = await _persistenceService.UnitOfWork.Items.QueryAsync(
                                      static item =>
                                          item.ParentContainerId == Serial.Zero &&
                                          item.EquippedMobileId == Serial.Zero &&
                                          item.TryGetCustomString(ItemCustomParamKeys.Item.TemplateId, out var templateId) &&
                                          string.Equals(templateId, PublicMoongateTemplateId, StringComparison.OrdinalIgnoreCase),
                                      static item => item.Id
                                  );

            var removed = 0;

            foreach (var gateId in existingGateIds)
            {
                if (await _itemService.DeleteItemAsync(gateId))
                {
                    removed++;
                }
            }

            var spawned = 0;

            foreach (var group in groups)
            {
                foreach (var destination in group.Destinations)
                {
                    var gate = _itemFactoryService.CreateItemFromTemplate(PublicMoongateTemplateId);
                    gate.MapId = destination.MapId;
                    gate.Location = destination.Location;

                    await _itemService.CreateItemAsync(gate);
                    _spatialWorldService.AddOrUpdateItem(gate, destination.MapId);
                    spawned++;
                }
            }

            context.Print(
                "Public moongate network rebuilt: removed {0}, spawned {1} across {2} groups.",
                removed,
                spawned,
                groups.Count
            );
        }
        catch (Exception ex)
        {
            context.PrintError("Failed to rebuild public moongate network: {0}", ex.Message);
        }
    }
}
