using DryIoc;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Network.Packets.Types.Targeting;
using Moongate.Server.Bootstrap.Internal;
using Moongate.Server.Data.Events.Targeting;
using Moongate.Server.Interfaces.Bootstrap;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Files;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Speech;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Interfaces.Templates;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Utils;

namespace Moongate.Server.Bootstrap.Phases;

/// <summary>
/// Bootstrap phase 3: wires file loaders, packet handlers, event listeners, and default commands.
/// </summary>
internal sealed class WiringPhase : IBootstrapPhase
{
    public int Order => 3;

    public string Name => "Wiring";

    public void Configure(BootstrapContext context)
    {
        RegisterFileLoaders(context);
        RegisterPacketHandlers(context);
        RegisterGameEventListeners(context);
        RegisterDefaultCommands(context);
    }

    private static void RegisterDefaultCommands(BootstrapContext context)
    {
        var commandService = context.Container.Resolve<ICommandSystemService>();

        commandService.RegisterCommand(
            "send_target",
            async commandContext =>
            {
                var eventBus = context.Container.Resolve<IGameEventBusService>();

                await eventBus.PublishAsync(
                    new TargetRequestCursorEvent(
                        commandContext.SessionId,
                        TargetCursorSelectionType.SelectLocation,
                        TargetCursorType.Helpful,
                        callback =>
                        {
                            commandContext.Print(
                                "Target cursor callback invoked with selection: {0} ",
                                callback.Packet.Location
                            );
                        }
                    )
                );
            },
            "Sends a target cursor to the specified player. Usage: send_target ",
            CommandSourceType.InGame,
            AccountType.Regular
        );

        commandService.RegisterCommand(
            "add_npc|.add_npc",
            async commandContext =>
            {
                if (commandContext.Arguments.Length != 1)
                {
                    commandContext.Print("Usage: .add_npc <templateId>");

                    return;
                }

                var templateId = commandContext.Arguments[0];
                var eventBus = context.Container.Resolve<IGameEventBusService>();
                var mobileService = context.Container.Resolve<IMobileService>();
                var mobileTemplateService = context.Container.Resolve<IMobileTemplateService>();
                var spatialWorldService = context.Container.Resolve<ISpatialWorldService>();
                var gameSessionService = context.Container.Resolve<IGameNetworkSessionService>();
                var characterService = context.Container.Resolve<ICharacterService>();

                if (!mobileTemplateService.TryGet(templateId, out _))
                {
                    commandContext.PrintError("Unknown mobile template: {0}", templateId);

                    return;
                }

                await eventBus.PublishAsync(
                    new TargetRequestCursorEvent(
                        commandContext.SessionId,
                        TargetCursorSelectionType.SelectLocation,
                        TargetCursorType.Helpful,
                        callback =>
                        {
                            try
                            {
                                if (!gameSessionService.TryGet(commandContext.SessionId, out var session))
                                {
                                    commandContext.PrintError("Cannot spawn NPC: session not found.");

                                    return;
                                }

                                var mapId = session.Character?.MapId ??
                                            characterService.GetCharacterAsync(session.CharacterId).GetAwaiter().GetResult()?.MapId ??
                                            1;

                                var mobile = mobileService
                                            .SpawnFromTemplateAsync(templateId, callback.Packet.Location, mapId)
                                            .GetAwaiter()
                                            .GetResult();

                                spatialWorldService.AddOrUpdateMobile(mobile);
                                commandContext.Print(
                                    "NPC '{0}' spawned at {1} (Map={2}, Serial={3}).",
                                    templateId,
                                    callback.Packet.Location,
                                    mapId,
                                    mobile.Id
                                );
                            }
                            catch (Exception ex)
                            {
                                commandContext.PrintError("Failed to spawn NPC '{0}': {1}", templateId, ex.Message);
                            }
                        }
                    )
                );
            },
            "Spawn an NPC from template at target location. Usage: .add_npc <templateId>",
            CommandSourceType.InGame,
            AccountType.Regular,
            _ =>
            {
                var mobileTemplateService = context.Container.Resolve<IMobileTemplateService>();

                return mobileTemplateService.GetAll()
                                            .Select(static template => template.Id)
                                            .Distinct(StringComparer.OrdinalIgnoreCase)
                                            .OrderBy(static id => id, StringComparer.OrdinalIgnoreCase)
                                            .ToArray();
            }
        );

        commandService.RegisterCommand(
            "orion",
            async commandContext =>
            {
                var eventBus = context.Container.Resolve<IGameEventBusService>();
                var mobileService = context.Container.Resolve<IMobileService>();
                var spatialWorldService = context.Container.Resolve<ISpatialWorldService>();

                await eventBus.PublishAsync(
                    new TargetRequestCursorEvent(
                        commandContext.SessionId,
                        TargetCursorSelectionType.SelectLocation,
                        TargetCursorType.Helpful,
                        callback =>
                        {
                            var mobile = mobileService.SpawnFromTemplateAsync("orione", callback.Packet.Location, 1)
                                                      .GetAwaiter()
                                                      .GetResult();

                            spatialWorldService.AddOrUpdateMobile(mobile);

                            commandContext.Print("Orion the cat: {0} ", callback.Packet.Location);
                        }
                    )
                );
            },
            "Create a cat, beautiful cat",
            CommandSourceType.InGame,
            AccountType.Regular
        );

        commandService.RegisterCommand(
            "add_item",
            (ctx =>
             {
                 var itemService = context.Container.Resolve<IItemService>();
                 var gameSessionService = context.Container.Resolve<IGameNetworkSessionService>();
                 var characterService = context.Container.Resolve<ICharacterService>();

                 var item = itemService.SpawnFromTemplateAsync("brick").GetAwaiter().GetResult();

                 if (gameSessionService.TryGet(ctx.SessionId, out var session))
                 {
                     var player = characterService.GetCharacterAsync(session.CharacterId).GetAwaiter().GetResult();
                     itemService.MoveItemToContainerAsync(item.Id, player.BackpackId, new Point2D(1, 1), ctx.SessionId)
                                .GetAwaiter()
                                .GetResult();
                     ctx.Print("Added a brick to your backpack.");
                 }
                 else
                 {
                     ctx.Print("Failed to add item: no active character found for your session.");
                 }

                 return Task.CompletedTask;
             }),
            "Add an item to your backpack",
            CommandSourceType.InGame,
            AccountType.Regular
        );

        commandService.RegisterCommand(
            "bank",
            async ctx =>
            {
                var gameSessionService = context.Container.Resolve<IGameNetworkSessionService>();
                var characterService = context.Container.Resolve<ICharacterService>();
                var itemService = context.Container.Resolve<IItemService>();
                var outgoingPacketQueue = context.Container.Resolve<IOutgoingPacketQueue>();

                if (!gameSessionService.TryGet(ctx.SessionId, out var session))
                {
                    ctx.Print("Failed to open bank: no active session found.");

                    return;
                }

                var character = await characterService.GetCharacterAsync(session.CharacterId);

                if (character is null)
                {
                    ctx.Print("Failed to open bank: character not found.");

                    return;
                }

                if (!character.EquippedItemIds.TryGetValue(ItemLayerType.Bank, out var bankId) ||
                    bankId == Moongate.UO.Data.Ids.Serial.Zero)
                {
                    ctx.Print("Failed to open bank: no bank box equipped.");

                    return;
                }

                var bank = await itemService.GetItemAsync(bankId);

                if (bank is null)
                {
                    ctx.Print("Failed to open bank: bank box not found.");

                    return;
                }

                outgoingPacketQueue.Enqueue(session.SessionId, new DrawContainerAndAddItemCombinedPacket(bank));
                ctx.Print("Bank box opened.");
            },
            "Open your bank box.",
            CommandSourceType.InGame,
            AccountType.Regular
        );

        commandService.RegisterCommand(
            "broadcast|bc",
            async ctx =>
            {
                if (ctx.Arguments.Length == 0)
                {
                    ctx.Print("Usage: broadcast <message>");

                    return;
                }

                var speechService = context.Container.Resolve<ISpeechService>();
                var message = string.Join(' ', ctx.Arguments);
                var recipients = await speechService.BroadcastFromServerAsync("SERVER: " + message, SpeechHues.Orange);

                ctx.Print("Broadcast sent to {0} session(s).", recipients);
            },
            "Send a server message to all active sessions. Usage: broadcast <message>",
            CommandSourceType.Console | CommandSourceType.InGame,
            AccountType.Administrator
        );
    }

    private static void RegisterFileLoaders(BootstrapContext context)
    {
        var fileLoaderService = context.Container.Resolve<IFileLoaderService>();
        BootstrapFileLoaderRegistration.Register(fileLoaderService);
    }

    private static void RegisterGameEventListeners(BootstrapContext context)
    {
        BootstrapGameEventListenerRegistration.Subscribe(context.Container);
    }

    private static void RegisterPacketHandlers(BootstrapContext context)
    {
        BootstrapPacketHandlerRegistration.Register(context.Container);
    }
}
