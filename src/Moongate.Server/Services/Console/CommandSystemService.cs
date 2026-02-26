using System.Text;
using Moongate.Network.Packets.Outgoing.Speech;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Events.Console;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Accounting;
using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Lifecycle;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Utils;
using Serilog;
using Serilog.Events;

namespace Moongate.Server.Services.Console;

/// <summary>
/// Implements registration and execution of built-in server commands.
/// </summary>
[RegisterGameEventListener]
public sealed class CommandSystemService : ICommandSystemService, IGameEventListener<CommandEnteredEvent>
{
    private readonly ILogger _logger = Log.ForContext<CommandSystemService>();
    private readonly Dictionary<string, CommandDefinition> _commands = new(StringComparer.OrdinalIgnoreCase);
    private readonly IConsoleUiService _consoleUiService;
    private readonly IGameEventBusService _gameEventBusService;
    private readonly IOutgoingPacketQueue _outgoingPacketQueue;
    private readonly IServerLifetimeService _serverLifetimeService;
    private readonly IAccountService _accountService;

    public CommandSystemService(
        IConsoleUiService consoleUiService,
        IGameEventBusService gameEventBusService,
        IOutgoingPacketQueue outgoingPacketQueue,
        IServerLifetimeService serverLifetimeService,
        IAccountService accountService
    )
    {
        _consoleUiService = consoleUiService;
        _gameEventBusService = gameEventBusService;
        _outgoingPacketQueue = outgoingPacketQueue;
        _serverLifetimeService = serverLifetimeService;
        _accountService = accountService;
        RegisterDefaultCommands();
    }

    public async Task ExecuteCommandAsync(
        string commandWithArgs,
        CommandSourceType source = CommandSourceType.Console,
        GameSession? session = null,
        CancellationToken cancellationToken = default
    )
    {
        _logger.Verbose("Received command input '{CommandInput}' from source {Source}", commandWithArgs, source);

        if (string.IsNullOrWhiteSpace(commandWithArgs))
        {
            _logger.Verbose("Ignoring empty command input from source {Source}", source);

            return;
        }

        var tokens = commandWithArgs
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (tokens.Length == 0)
        {
            _logger.Verbose("Ignoring command input with no tokens from source {Source}", source);

            return;
        }

        var command = tokens[0].ToLowerInvariant();
        _logger.Verbose(
            "Parsed command '{Command}' with {ArgumentCount} args from source {Source}",
            command,
            tokens.Length - 1,
            source
        );

        if (!_commands.TryGetValue(command, out var commandDefinition))
        {
            _logger.Verbose("Command '{Command}' is not registered", command);
            WriteCommandOutput(source, session, LogEventLevel.Warning, "Unknown command: {0}", commandWithArgs);

            return;
        }

        if (!commandDefinition.Source.HasFlag(source))
        {
            _logger.Verbose(
                "Command '{Command}' is not allowed for source {Source}. Allowed source flags: {AllowedSource}",
                command,
                source,
                commandDefinition.Source
            );
            WriteCommandOutput(
                source,
                session,
                LogEventLevel.Warning,
                "Command '{0}' is not available from source '{1}'.",
                command,
                source
            );

            return;
        }

        var invokerAccountType = ResolveInvokerAccountType(source, session);

        if (invokerAccountType < commandDefinition.MinimumAccountType)
        {
            _logger.Verbose(
                "Command '{Command}' requires account type {RequiredAccountType}, but invoker has {InvokerAccountType}",
                command,
                commandDefinition.MinimumAccountType,
                invokerAccountType
            );
            WriteCommandOutput(
                source,
                session,
                LogEventLevel.Warning,
                "Command '{0}' requires account type '{1}'.",
                command,
                commandDefinition.MinimumAccountType
            );

            return;
        }

        var context = new CommandSystemContext(
            commandWithArgs,
            [.. tokens.Skip(1)],
            source,
            session?.SessionId ?? -1,
            (message, level) => WriteCommandOutput(source, session, level, message)
        );

        _logger.Verbose("Executing command handler for '{Command}'", command);

        try
        {
            await commandDefinition.Handler(context);
            _logger.Verbose("Command '{Command}' executed successfully", command);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Command '{Command}' execution failed", command);
            WriteCommandOutput(
                source,
                session,
                LogEventLevel.Error,
                "Command '{0}' failed. Check logs for details.",
                command
            );
        }
    }

    public IReadOnlyList<string> GetAutocompleteSuggestions(string commandWithArgs)
    {
        if (commandWithArgs.Length == 0)
        {
            return _commands.Keys
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .OrderBy(static k => k, StringComparer.OrdinalIgnoreCase)
                            .ToArray();
        }

        var firstSpaceIndex = commandWithArgs.IndexOf(' ');

        if (firstSpaceIndex < 0)
        {
            return _commands.Keys
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .Where(key => key.StartsWith(commandWithArgs, StringComparison.OrdinalIgnoreCase))
                            .OrderBy(static k => k, StringComparer.OrdinalIgnoreCase)
                            .ToArray();
        }

        var commandToken = commandWithArgs[..firstSpaceIndex];

        if (!_commands.TryGetValue(commandToken, out var commandDefinition))
        {
            return _commands.Keys
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .Where(key => key.StartsWith(commandToken, StringComparison.OrdinalIgnoreCase))
                            .OrderBy(static k => k, StringComparer.OrdinalIgnoreCase)
                            .ToArray();
        }

        if (commandDefinition.AutocompleteProvider is null)
        {
            return [];
        }

        var argumentText = commandWithArgs[(firstSpaceIndex + 1)..];
        var endsWithWhitespace = commandWithArgs.Length > 0 && char.IsWhiteSpace(commandWithArgs[^1]);
        var arguments = argumentText.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
        );

        var context = new CommandAutocompleteContext
        {
            CommandName = commandToken,
            Arguments = arguments,
            EndsWithWhitespace = endsWithWhitespace
        };

        var providerSuggestions = commandDefinition.AutocompleteProvider(context);

        if (providerSuggestions.Count == 0)
        {
            return [];
        }

        var prefix = string.Empty;
        var stableArgs = arguments;

        if (!endsWithWhitespace && arguments.Length > 0)
        {
            prefix = arguments[^1];
            stableArgs = arguments[..^1];
        }

        return providerSuggestions
               .Where(static suggestion => !string.IsNullOrWhiteSpace(suggestion))
               .Where(suggestion => suggestion.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
               .Select(suggestion => BuildAutocompleteLine(commandToken, stableArgs, suggestion))
               .Distinct(StringComparer.OrdinalIgnoreCase)
               .OrderBy(static suggestion => suggestion, StringComparer.OrdinalIgnoreCase)
               .ToArray();
    }

    public async Task HandleAsync(CommandEnteredEvent gameEvent, CancellationToken cancellationToken = default)
    {
        await ExecuteCommandAsync(
            gameEvent.CommandText,
            gameEvent.Source,
            gameEvent.GameSession,
            cancellationToken
        );
    }

    public void RegisterCommand(
        string commandName,
        Func<CommandSystemContext, Task> handler,
        string description = "",
        CommandSourceType source = CommandSourceType.Console,
        AccountType minimumAccountType = AccountType.Administrator,
        Func<CommandAutocompleteContext, IReadOnlyList<string>>? autocompleteProvider = null
    )
    {
        if (string.IsNullOrWhiteSpace(commandName))
        {
            throw new ArgumentException("Command name is required.", nameof(commandName));
        }

        var aliases = commandName.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var commandDefinition = new CommandDefinition
        {
            Name = aliases[0].Trim().ToLowerInvariant(),
            Description = description,
            Handler = handler,
            Source = source,
            MinimumAccountType = minimumAccountType,
            AutocompleteProvider = autocompleteProvider
        };

        foreach (var alias in aliases)
        {
            var normalizedAlias = alias.Trim().ToLowerInvariant();

            if (!_commands.TryAdd(normalizedAlias, commandDefinition))
            {
                _logger.Warning("Command '{CommandName}' is already registered.", normalizedAlias);

                continue;
            }

            _logger.Debug("Registered command {CommandName}", normalizedAlias);
        }
    }

    public Task StartAsync()
    {
        _gameEventBusService.RegisterListener(this);
        _logger.Information("Command system started with {CommandCount} command aliases.", _commands.Count);

        return Task.CompletedTask;
    }

    public Task StopAsync()
        => Task.CompletedTask;

    private static string BuildAutocompleteLine(string commandToken, string[] stableArgs, string suggestion)
    {
        if (stableArgs.Length == 0)
        {
            return $"{commandToken} {suggestion}";
        }

        return $"{commandToken} {string.Join(' ', stableArgs)} {suggestion}";
    }

    private Task OnExitCommand(CommandSystemContext context)
    {
        context.Print("Shutdown requested by console command.");
        _serverLifetimeService.RequestShutdown();

        return Task.CompletedTask;
    }

    private Task OnHelpCommand(CommandSystemContext context)
    {
        if (context.Arguments.Length > 0)
        {
            var requestedCommand = context.Arguments[0];

            if (!_commands.TryGetValue(requestedCommand, out var requestedDefinition))
            {
                context.Print("No help found for: {0}", requestedCommand);

                return Task.CompletedTask;
            }

            context.Print("{0}: {1}", requestedDefinition.Name, requestedDefinition.Description);

            return Task.CompletedTask;
        }

        var uniqueCommands = _commands
                             .Values
                             .Distinct()
                             .OrderBy(command => command.Name, StringComparer.OrdinalIgnoreCase)
                             .ToArray();

        var builder = new StringBuilder();
        builder.AppendLine("Available commands:");

        foreach (var command in uniqueCommands)
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

    private Task OnLockCommand(CommandSystemContext context)
    {
        _consoleUiService.LockInput();
        context.Print(
            "Console input is locked. Press '{0}' to unlock.",
            _consoleUiService.UnlockCharacter
        );

        return Task.CompletedTask;
    }

    private async Task OnAddUserCommand(CommandSystemContext context)
    {
        if (context.Arguments.Length is < 3 or > 4)
        {
            context.Print("Usage: add_user <username> <password> <email> [level]");

            return;
        }

        var username = context.Arguments[0];
        var password = context.Arguments[1];
        var email = context.Arguments[2];
        var level = AccountType.Regular;

        if (context.Arguments.Length == 4 &&
            !Enum.TryParse(context.Arguments[3], true, out level))
        {
            var validLevels = string.Join(", ", Enum.GetNames<AccountType>());
            context.Print("Invalid account level '{0}'. Valid levels: {1}.", context.Arguments[3], validLevels);

            return;
        }

        if (await _accountService.CheckAccountExistsAsync(username))
        {
            context.Print("User '{0}' already exists.", username);

            return;
        }

        await _accountService.CreateAccountAsync(username, password, email, level);
        context.Print("User '{0}' created with level '{1}'.", username, level);
    }

    private void RegisterDefaultCommands()
    {
        RegisterCommand(
            "help|?",
            OnHelpCommand,
            "Displays available commands.",
            CommandSourceType.Console | CommandSourceType.InGame,
            AccountType.Regular
        );
        RegisterCommand(
            "lock|*",
            OnLockCommand,
            "Locks console input. Press '*' to unlock."
        );
        RegisterCommand(
            "exit|shutdown",
            OnExitCommand,
            "Requests server shutdown."
        );
        RegisterCommand(
            "add_user",
            OnAddUserCommand,
            "Creates a new account: add_user <username> <password> <email> [level].",
            CommandSourceType.Console | CommandSourceType.InGame,
            AccountType.Administrator
        );
    }

    private static AccountType ResolveInvokerAccountType(CommandSourceType source, GameSession? session)
    {
        if (source == CommandSourceType.Console)
        {
            return AccountType.Administrator;
        }

        if (source == CommandSourceType.InGame && session is not null)
        {
            return session.AccountType;
        }

        return AccountType.Regular;
    }

    private void WriteCommandOutput(
        CommandSourceType source,
        GameSession? session,
        LogEventLevel level,
        string message,
        params object[] args
    )
    {
        var formatted = args.Length == 0 ? message : string.Format(message, args);

        if (source == CommandSourceType.InGame && session is not null)
        {
            WriteInGameOutput(session, formatted, level);

            return;
        }

        _consoleUiService.WriteLogLine(formatted, level);
    }

    private void WriteInGameOutput(GameSession session, string formatted, LogEventLevel level)
    {
        var hue = level switch
        {
            LogEventLevel.Error or LogEventLevel.Fatal => SpeechHues.Red,
            LogEventLevel.Warning                      => SpeechHues.Yellow,
            _                                          => SpeechHues.System
        };

        var lines = formatted.Split(['\n'], StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var normalized = line.TrimEnd('\r');

            if (normalized.Length == 0)
            {
                continue;
            }

            _outgoingPacketQueue.Enqueue(session.SessionId, SpeechMessageFactory.CreateSystem(normalized, hue));
        }
    }
}
