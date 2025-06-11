using Moongate.Core.Server.Data.Internal.Commands;
using Moongate.Core.Server.Interfaces.Services;
using Moongate.Core.Server.Types;
using Moongate.UO.Interfaces.Services;
using Serilog;
using Spectre.Console;

namespace Moongate.Server.Services;

public class CommandSystemService : ICommandSystemService
{
    private const string _prompt = "[blue]Moongate> [/]";
    private const char _unlockCharacter = '*';
    private bool _isConsoleLocked = true;

    private readonly ILogger _logger = Log.ForContext<CommandSystemService>();
    private readonly Dictionary<string, CommandDefinition> _commands = new();
    private readonly IGameSessionService _gameSessionService;


    private readonly List<string> _commandHistory = new();

    private string _inputBuffer = string.Empty;

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
        var context = CreateCommandContext(commandWithArgs, sessionId, source);
        if (string.IsNullOrEmpty(command) || !_commands.TryGetValue(command, out var commandDefinition))
        {
            context.Print("Unknown command: {0}", commandWithArgs);
            return;
        }

        if (source == CommandSourceType.Console)
        {
            await commandDefinition.Handler(context);

            return;
        }

        // TODO: Check if the session exists and if the account level is sufficient

        var session = _gameSessionService.GetSession(sessionId);

        await commandDefinition.Handler(context);
    }

    private async Task HookConsoleCommandAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (Console.KeyAvailable)
                {
                    var keyInfo = Console.ReadKey(true);
                    if (_isConsoleLocked)
                    {
                        if (keyInfo.KeyChar == _unlockCharacter)
                        {
                            _isConsoleLocked = false;
                            AnsiConsole.Markup("[green]Console unlocked.[/]");
                            Console.WriteLine();
                            AnsiConsole.Markup(_prompt);
                        }
                        else
                        {
                            AnsiConsole.MarkupLine($"[red]Console is locked. Press {_unlockCharacter} to unlock  [/]");
                        }

                        continue;
                    }

                    await ProcessKeyPress(keyInfo);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error processing console input.");
                AnsiConsole.Markup("[red]An error occurred while processing input. Please try again. [/]");
            }


            await Task.Delay(10, cancellationToken);
        }
    }

    public async Task StartConsoleAsync(CancellationToken cancellationToken)
    {
        _ = Task.Run(
            async () =>
            {
                AnsiConsole.Markup(_prompt);
                await HookConsoleCommandAsync(cancellationToken);
            },
            cancellationToken
        );
    }

    private async Task ProcessKeyPress(ConsoleKeyInfo keyInfo)
    {
        switch (keyInfo.Key)
        {
            case ConsoleKey.Backspace:
                HandleBackspace();
                break;

            case ConsoleKey.Enter:
                await HandleEnterKey();
                AnsiConsole.Markup(_prompt);
                break;

            case ConsoleKey.Tab:
                break;

            default:
                HandleCharacterInput(keyInfo.KeyChar);
                break;
        }
    }

    private void HandleBackspace()
    {
        if (_inputBuffer.Length > 0)
        {
            _inputBuffer = _inputBuffer[..^1];
            Console.Write("\b \b");
        }
    }

    private void HandleCharacterInput(char character)
    {
        _inputBuffer += character;
        Console.Write(character);
    }

    private async Task HandleEnterKey()
    {
        if (string.IsNullOrWhiteSpace(_inputBuffer))
        {
            return;
        }

        Console.Write(Environment.NewLine);

        var command = _inputBuffer.Trim();
        _commandHistory.Add(command);
        _inputBuffer = string.Empty;

        await ExecuteCommandAsync(command, string.Empty, AccountLevelType.Admin, CommandSourceType.Console);
    }

    private CommandSystemContext CreateCommandContext(
        string command, string sessionId, CommandSourceType source
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
