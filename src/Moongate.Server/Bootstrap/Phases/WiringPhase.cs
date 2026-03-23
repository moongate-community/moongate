using DryIoc;
using Moongate.Server.Bootstrap.Internal;
using Moongate.Server.Interfaces.Bootstrap;
using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Interfaces.Services.Files;

namespace Moongate.Server.Bootstrap.Phases;

/// <summary>
/// Bootstrap phase 3: wires file loaders, packet handlers, event listeners, and generated command registrations.
/// </summary>
internal sealed class WiringPhase : IBootstrapPhase
{
    public int Order => 4;

    public string Name => "Wiring";

    public void Configure(BootstrapContext context)
    {
        RegisterFileLoaders(context);
        RegisterPacketHandlers(context);
        RegisterGameEventListeners(context);
        RegisterCommands(context);
    }

    private static void RegisterCommands(BootstrapContext context)
    {
        var commandSystemService = context.Container.Resolve<ICommandSystemService>();
        BootstrapConsoleCommandRegistration.RegisterCommands(context.Container, commandSystemService);
        BootstrapConsoleCommandRegistration.RegisterCommands(
            context.Container,
            commandSystemService,
            context.PluginRegistrations.ConsoleCommandTypes
        );
    }

    private static void RegisterFileLoaders(BootstrapContext context)
    {
        var fileLoaderService = context.Container.Resolve<IFileLoaderService>();
        BootstrapFileLoaderRegistration.Register(fileLoaderService);
        BootstrapFileLoaderRegistration.Register(fileLoaderService, context.PluginRegistrations.FileLoaderTypes);
    }

    private static void RegisterGameEventListeners(BootstrapContext context)
    {
        BootstrapGameEventListenerRegistration.Subscribe(context.Container);
        BootstrapGameEventListenerRegistration.Subscribe(
            context.Container,
            context.PluginRegistrations.GameEventListenerTypes
        );
    }

    private static void RegisterPacketHandlers(BootstrapContext context)
        => BootstrapPacketHandlerRegistration.Register(context.Container, context.PluginRegistrations.PacketHandlerTypes);
}
