using Moongate.Server.Core.Commands;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Core.Types.Commands;

namespace Moongate.Server.Commands;

/// <summary>
///     Lists commands available to the caller and describes a command by name or alias.
/// </summary>
public sealed class HelpCommand : ICommandExecutor
{
    private readonly CommandRegistry _registry;

    public HelpCommand(CommandRegistry registry)
    {
        _registry = registry;
    }

    /// <inheritdoc />
    public Task ExecuteAsync(CommandContext context)
    {
        if (context.Arguments.Length > 1)
        {
            context.PrintError("Usage: help [command]");

            return Task.CompletedTask;
        }

        var available = _registry.Registrations
            .Values
            .Select(registration => registration.Definition)
            .Distinct()
            .Where(definition => IsAvailable(definition, context))
            .OrderBy(definition => definition.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (context.Arguments.Length == 0)
        {
            context.Print("Available commands:");

            foreach (var definition in available)
            {
                context.Print("{0} - {1}", definition.Name, definition.Description);
            }

            return Task.CompletedTask;
        }

        var name = context.Arguments[0];
        var command =
            available.FirstOrDefault(definition => definition.Aliases.Contains(name, StringComparer.OrdinalIgnoreCase)
            );

        if (command is null)
        {
            context.PrintError("Unknown or unavailable command: {0}", name);

            return Task.CompletedTask;
        }

        context.Print("Command: {0}", command.Name);
        context.Print("Description: {0}", command.Description);
        context.Print("Aliases: {0}", string.Join(", ", command.Aliases));
        context.Print("Sources: {0}", command.Source);
        context.Print("Minimum account level: {0}", command.MinimumAccountType);

        return Task.CompletedTask;
    }

    private static bool IsAvailable(CommandDefinition definition, CommandContext context)
    {
        if (context.Source == CommandSourceType.None || !definition.Source.HasFlag(context.Source))
        {
            return false;
        }

        var level = context.Source switch
        {
            CommandSourceType.Console => AccountType.Administrator,
            CommandSourceType.InGame  => context.Session?.AccountType ?? AccountType.Regular,
            _                         => AccountType.Regular
        };

        return level >= definition.MinimumAccountType;
    }
}
