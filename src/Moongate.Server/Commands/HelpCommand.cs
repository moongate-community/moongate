using System.Text;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Commands;

/// <summary>
/// Displays command help information.
/// </summary>
[RegisterConsoleCommand(
    "help|?",
    "Displays available commands.",
    CommandSourceType.Console | CommandSourceType.InGame,
    AccountType.Regular
)]
public sealed class HelpCommand : ICommandExecutor
{
    private readonly ICommandSystemService _commandSystemService;

    public HelpCommand(ICommandSystemService commandSystemService)
    {
        _commandSystemService = commandSystemService;
    }

    public Task ExecuteCommandAsync(CommandSystemContext context)
    {
        var commands = _commandSystemService.GetRegisteredCommands();

        if (context.Arguments.Length > 0)
        {
            var requestedCommand = context.Arguments[0];
            var requestedDefinition = commands.FirstOrDefault(
                definition => definition.Name.Equals(requestedCommand, StringComparison.OrdinalIgnoreCase)
            );

            if (requestedDefinition is null)
            {
                context.Print("No help found for: {0}", requestedCommand);

                return Task.CompletedTask;
            }

            context.Print("{0}: {1}", requestedDefinition.Name, requestedDefinition.Description);

            return Task.CompletedTask;
        }

        var builder = new StringBuilder();
        builder.AppendLine("Available commands:");

        foreach (var command in commands)
        {
            builder.Append("- ");
            builder.Append(command.Name);

            if (!string.IsNullOrWhiteSpace(command.Description))
            {
                builder.Append(": ");
                builder.Append(command.Description);
            }

            builder.AppendLine();
        }

        context.Print(builder.ToString().TrimEnd());

        return Task.CompletedTask;
    }
}
