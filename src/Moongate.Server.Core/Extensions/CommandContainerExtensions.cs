using DryIoc;
using Moongate.Server.Core.Commands;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Core.Types.Commands;

namespace Moongate.Server.Core.Extensions;

/// <summary>
///     Registers container-owned singleton command executors without resolving dependencies.
/// </summary>
public static class CommandContainerExtensions
{
    /// <summary>
    ///     Registers one command, its aliases and its deferred binder before command system startup.
    /// </summary>
    public static Container RegisterCommand<TExecutor>(
        this Container container,
        string commandName,
        string description = "",
        CommandSourceType source = CommandSourceType.Console,
        AccountType minimumAccountType = AccountType.Administrator
    )
        where TExecutor : class, ICommandExecutor
    {
        if (!container.IsRegistered<CommandRegistry>())
        {
            container.RegisterInstance(new CommandRegistry());
        }

        container.Resolve<CommandRegistry>()
            .Register<TExecutor>(container, commandName, description, source, minimumAccountType);

        return container;
    }
}
