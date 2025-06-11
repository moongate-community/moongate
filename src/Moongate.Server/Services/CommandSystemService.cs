using Moongate.Core.Server.Data.Internal.Commands;
using Moongate.Core.Server.Interfaces.Services;
using Moongate.Core.Server.Types;
using Moongate.UO.Interfaces.Services;
using Serilog;

namespace Moongate.Server.Services;

public class CommandSystemService : ICommandSystemService
{
    private readonly ILogger _logger = Log.ForContext<CommandSystemService>();
    private readonly Dictionary<string, CommandDefinition> _commands = new();
    private readonly IGameSessionService _gameSessionService;

    public CommandSystemService(IGameSessionService gameSessionService)
    {
        _gameSessionService = gameSessionService;
    }

    public void Dispose()
    {
    }

    public void RegisterCommand(
        string commandName, ICommandSystemService.CommandHandlerDelegate handler, string description = "",
        AccountLevelType accountLevel = AccountLevelType.User, CommandSourceType source = CommandSourceType.InGame
    )
    {
        foreach (var splitCommand in commandName.Split(','))
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

    public async Task ExecuteCommandAsync(
        string commandWithArgs, string sessionId, AccountLevelType accountLevel, CommandSourceType source
    )
    {
        var command = commandWithArgs.Split(' ').FirstOrDefault()?.ToLowerInvariant();
        var context = CreateCommandContext(commandWithArgs, sessionId , source);
        if (string.IsNullOrEmpty(command) || !_commands.TryGetValue(command, out var commandDefinition))
        {
            _logger.Warning("Command '{Command}' not found or not registered.", command);
            return;
        }

        if (source == CommandSourceType.Console)
        {
            await commandDefinition.Handler(context);

            return;
        }




    }

    private CommandSystemContext CreateCommandContext(
        string command, string sessionId,  CommandSourceType source
    )
    {
        var context = new CommandSystemContext
        {
            Command = command,
            SourceType = source,
            Arguments = command.Split(' ').Skip(1).ToArray(),
        };
        if (string.IsNullOrEmpty(sessionId))
        {
            context.OnPrint += OnPrintToConsole;
        }

        return context;
    }

    private void OnPrintToConsole(string message, object[] args)
    {
        Console.WriteLine(message, args);
    }
}
