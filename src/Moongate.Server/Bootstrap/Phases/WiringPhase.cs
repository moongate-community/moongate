using DryIoc;
using Moongate.Network.Packets.Types.Targeting;
using Moongate.Server.Bootstrap.Internal;
using Moongate.Server.Data.Events.Targeting;
using Moongate.Server.Interfaces.Bootstrap;
using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Files;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Types;

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
