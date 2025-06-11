using Moongate.Core.Server.Data.Internal.Commands;
using Moongate.Core.Server.Interfaces.Services;
using Moongate.Core.Server.Types;
using Serilog;

namespace Moongate.Core.Server.Services;

public class CommandSystemService : ICommandSystemService
{
    private readonly ILogger _logger = Log.ForContext<CommandSystemService>();
    private readonly Dictionary<string, CommandDefinition> _commands = new();
    public void Dispose()
    {
    }

    public void RegisterCommand(
        string commandName, ICommandSystemService.CommandHandlerDelegate handler, string description = "",
        AccountLevelType accountLevel = AccountLevelType.User, CommandSourceType source = CommandSourceType.InGame
    )
    {
        foreach(var splitCommand in commandName.Split(','))
        {
            var trimmedCommand = splitCommand.Trim().ToLowerInvariant();
            if (_commands.ContainsKey(trimmedCommand))
            {
                _logger.Warning("Command '{CommandName}' is already registered.", trimmedCommand);
                continue;
            }

            var commandDefinition = new CommandDefinition
            {
                Name = trimmedCommand,
                Description = description,
                AccountLevel = accountLevel,
                Source = source,
                Handler = handler
            };

            _commands[trimmedCommand] = commandDefinition;
            _logger.Information("Registered command: {CommandName}", trimmedCommand);
        }
    }
}
