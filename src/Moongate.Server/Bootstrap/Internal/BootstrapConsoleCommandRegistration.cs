using DryIoc;
using Moongate.Server.Interfaces.Services.Console;

namespace Moongate.Server.Bootstrap.Internal;

/// <summary>
/// Registers DI services and command bindings for command executors discovered by source generators.
/// </summary>
internal static partial class BootstrapConsoleCommandRegistration
{
    public static void RegisterServices(Container container)
        => RegisterServicesGenerated(container);

    public static void RegisterCommands(Container container, ICommandSystemService commandSystemService)
        => RegisterCommandsGenerated(container, commandSystemService);

    static partial void RegisterServicesGenerated(Container container);

    static partial void RegisterCommandsGenerated(Container container, ICommandSystemService commandSystemService);
}
