using DryIoc;
using Moongate.Server.Attributes;
using Moongate.Server.Interfaces.Services.Console;

namespace Moongate.Server.Bootstrap.Internal;

/// <summary>
/// Registers DI services and command bindings for command executors discovered by source generators.
/// </summary>
internal static partial class BootstrapConsoleCommandRegistration
{
    public static void RegisterCommands(Container container, ICommandSystemService commandSystemService)
        => RegisterCommandsGenerated(container, commandSystemService);

    public static void RegisterCommands(
        Container container,
        ICommandSystemService commandSystemService,
        IEnumerable<Type> pluginCommandTypes
    )
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(commandSystemService);
        ArgumentNullException.ThrowIfNull(pluginCommandTypes);

        foreach (var commandType in pluginCommandTypes)
        {
            RegisterCommand(container, commandSystemService, commandType);
        }
    }

    public static void RegisterServices(Container container)
        => RegisterServicesGenerated(container);

    public static void RegisterServices(Container container, IEnumerable<Type> pluginCommandTypes)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(pluginCommandTypes);

        foreach (var commandType in pluginCommandTypes)
        {
            if (!container.IsRegistered(commandType))
            {
                container.Register(commandType, Reuse.Singleton);
            }
        }
    }

    private static void RegisterCommand(Container container, ICommandSystemService commandSystemService, Type commandType)
    {
        var attribute = commandType.GetCustomAttributes(typeof(RegisterConsoleCommandAttribute), false)
                                   .Cast<RegisterConsoleCommandAttribute>()
                                   .FirstOrDefault();

        if (attribute is null)
        {
            throw new InvalidOperationException(
                $"Plugin command type '{commandType.FullName}' is missing RegisterConsoleCommandAttribute."
            );
        }

        if (!typeof(ICommandExecutor).IsAssignableFrom(commandType))
        {
            throw new InvalidOperationException(
                $"Plugin command type '{commandType.FullName}' does not implement ICommandExecutor."
            );
        }

        if (!container.IsRegistered(commandType))
        {
            container.Register(commandType, Reuse.Singleton);
        }

        var executor = (ICommandExecutor)container.Resolve(commandType);

        commandSystemService.RegisterCommand(
            attribute.CommandName,
            executor.ExecuteCommandAsync,
            attribute.Description,
            attribute.Source,
            attribute.MinimumAccountType,
            executor.AutocompleteProvider
        );
    }

    static partial void RegisterCommandsGenerated(Container container, ICommandSystemService commandSystemService);

    static partial void RegisterServicesGenerated(Container container);
}
