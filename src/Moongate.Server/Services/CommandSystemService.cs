using System.Text;
using DryIoc;
using Moongate.Core.Server.Bootstrap;
using Moongate.Core.Server.Data.Internal.Commands;
using Moongate.Core.Server.Instances;
using Moongate.Core.Server.Interfaces.Services;
using Moongate.Core.Server.Types;
using Moongate.UO.Data.Types;
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

    private int _commandHistoryIndex = -1;

    private string _inputBuffer = string.Empty;

    public CommandSystemService(IGameSessionService gameSessionService)
    {
        _gameSessionService = gameSessionService;

        RegisterDefaultCommands();
    }

    public void Dispose() { }

    public async Task ExecuteCommandAsync(
        string commandWithArgs,
        string sessionId,
        AccountLevelType accountLevel,
        CommandSourceType source
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

        await commandDefinition.Handler(context);
    }

    public void RegisterCommand(
        string commandName,
        ICommandSystemService.CommandHandlerDelegate handler,
        string description = "",
        AccountLevelType accountLevel = AccountLevelType.User,
        CommandSourceType source = CommandSourceType.InGame
    )
    {
        foreach (var splitCommand in commandName.Split('|'))
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
            _logger.Debug("Registered command: {CommandName}", trimmedCommand);
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

    private CommandSystemContext CreateCommandContext(string command, string sessionId, CommandSourceType source)
    {
        var context = new CommandSystemContext
        {
            Command = command,
            SourceType = source,
            Arguments = command.Split(' ').Skip(1).ToArray()
        };

        if (string.IsNullOrEmpty(sessionId))
        {
            context.OnPrint += OnPrintToConsole;
        }
        else
        {
            context.OnPrint += OnPrintToMobile;
            context.SessionId = sessionId;
        }

        return context;
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

    private async Task OnExitCommand(CommandSystemContext context)
    {
        AnsiConsole.Markup("[red]Exiting application in 5 seconds...[/]");
        var bootstrap = MoongateContext.Container.Resolve<MoongateBootstrap>();
        await Task.Delay(5000);
        await bootstrap.RequestShutdownAsync();
    }

    private Task OnHelpCommand(CommandSystemContext context)
    {
        // TODO: Show only commands available to the user
        var helpMessage = new StringBuilder();
        helpMessage.AppendLine("Available commands:");

        foreach (var command in _commands.Values.Where(s => s.Source.HasFlag(context.SourceType)))
        {
            helpMessage.AppendLine($"- {command.Name}: {command.Description}");
        }

        context.Print(helpMessage.ToString());

        return Task.CompletedTask;
    }

    private Task OnLockCommand(CommandSystemContext context)
    {
        _isConsoleLocked = true;
        AnsiConsole.Markup("[red]Console locked. Press '*' to unlock.[/]");
        Console.WriteLine();

        return Task.CompletedTask;
    }

    private void OnPrintToConsole(string sessionId, string message, object[] args)
    {
        Console.WriteLine(message, args);
    }

    private void OnPrintToMobile(string sessionId, string message, object[] args)
    {
        var gameSession = _gameSessionService.GetSession(sessionId);

        gameSession.Mobile.ReceiveSpeech(null, ChatMessageType.System, 0, string.Format(message, args), 0, 3);
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

            case ConsoleKey.UpArrow:
                if (_isConsoleLocked)
                {
                    break;
                }

                if (_commandHistoryIndex < _commandHistory.Count - 1)
                {
                    _commandHistoryIndex++;
                    _inputBuffer = _commandHistory[_commandHistory.Count - 1 - _commandHistoryIndex];
                    AnsiConsole.Markup($"\r{_prompt}{_inputBuffer}");
                }

                break;

            case ConsoleKey.DownArrow:
                if (_isConsoleLocked)
                {
                    break;
                }

                if (_commandHistoryIndex > 0)
                {
                    _commandHistoryIndex--;
                    _inputBuffer = _commandHistory.Count > _commandHistoryIndex
                                       ? _commandHistory[_commandHistory.Count - 1 - _commandHistoryIndex]
                                       : string.Empty;
                    AnsiConsole.Markup($"\r{_prompt}{_inputBuffer}");
                }
                else
                {
                    _commandHistoryIndex = -1;
                    _inputBuffer = string.Empty;
                    AnsiConsole.Markup($"\r{_prompt}");
                }

                break;

            default:
                HandleCharacterInput(keyInfo.KeyChar);

                break;
        }
    }

    private void RegisterDefaultCommands()
    {
        RegisterCommand("help|?", OnHelpCommand, "Displays this help message.");
        RegisterCommand(
            "lock|" + _unlockCharacter,
            OnLockCommand,
            "Locks the console input. Press '*' to unlock.",
            AccountLevelType.Admin,
            CommandSourceType.Console
        );
        RegisterCommand(
            "exit|shutdown",
            OnExitCommand,
            "Exits the application.",
            AccountLevelType.Admin,
            CommandSourceType.Console
        );
    }
}
