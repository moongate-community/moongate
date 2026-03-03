using Moongate.Network.Packets.Types.Targeting;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Events.Targeting;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Interfaces.Templates;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Commands;

/// <summary>
/// Spawns an NPC from template at target location.
/// </summary>
[RegisterConsoleCommand(
    "add_npc|.add_npc",
    "Spawn an NPC from template at target location. Usage: .add_npc <templateId>",
    CommandSourceType.InGame,
    AccountType.Regular
)]
public sealed class AddNpcCommand : ICommandExecutor
{
    private readonly IGameEventBusService _gameEventBusService;
    private readonly IMobileService _mobileService;
    private readonly IMobileTemplateService _mobileTemplateService;
    private readonly ISpatialWorldService _spatialWorldService;
    private readonly IGameNetworkSessionService _gameSessionService;
    private readonly ICharacterService _characterService;

    public AddNpcCommand(
        IGameEventBusService gameEventBusService,
        IMobileService mobileService,
        IMobileTemplateService mobileTemplateService,
        ISpatialWorldService spatialWorldService,
        IGameNetworkSessionService gameSessionService,
        ICharacterService characterService
    )
    {
        _gameEventBusService = gameEventBusService;
        _mobileService = mobileService;
        _mobileTemplateService = mobileTemplateService;
        _spatialWorldService = spatialWorldService;
        _gameSessionService = gameSessionService;
        _characterService = characterService;
    }

    public Func<CommandAutocompleteContext, IReadOnlyList<string>>? AutocompleteProvider
        => _ => _mobileTemplateService.GetAll()
                                      .Select(static template => template.Id)
                                      .Distinct(StringComparer.OrdinalIgnoreCase)
                                      .OrderBy(static id => id, StringComparer.OrdinalIgnoreCase)
                                      .ToArray();

    public async Task ExecuteCommandAsync(CommandSystemContext context)
    {
        if (context.Arguments.Length != 1)
        {
            context.Print("Usage: .add_npc <templateId>");

            return;
        }

        var templateId = context.Arguments[0];

        if (!_mobileTemplateService.TryGet(templateId, out _))
        {
            context.PrintError("Unknown mobile template: {0}", templateId);

            return;
        }

        await _gameEventBusService.PublishAsync(
            new TargetRequestCursorEvent(
                context.SessionId,
                TargetCursorSelectionType.SelectLocation,
                TargetCursorType.Helpful,
                callback =>
                {
                    try
                    {
                        if (!_gameSessionService.TryGet(context.SessionId, out var session))
                        {
                            context.PrintError("Cannot spawn NPC: session not found.");

                            return;
                        }

                        var mapId = session.Character?.MapId ??
                                    _characterService.GetCharacterAsync(session.CharacterId)
                                                     .GetAwaiter()
                                                     .GetResult()
                                                     ?.MapId ??
                                    1;

                        var mobile = _mobileService
                                     .SpawnFromTemplateAsync(templateId, callback.Packet.Location, mapId)
                                     .GetAwaiter()
                                     .GetResult();

                        _spatialWorldService.AddOrUpdateMobile(mobile);
                        context.Print(
                            "NPC '{0}' spawned at {1} (Map={2}, Serial={3}).",
                            templateId,
                            callback.Packet.Location,
                            mapId,
                            mobile.Id
                        );
                    }
                    catch (Exception ex)
                    {
                        context.PrintError("Failed to spawn NPC '{0}': {1}", templateId, ex.Message);
                    }
                }
            )
        );
    }
}
