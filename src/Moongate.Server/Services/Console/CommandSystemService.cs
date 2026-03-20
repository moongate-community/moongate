using Moongate.Abstractions.Types;
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
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Utils;
using Serilog;
using Serilog.Events;

namespace Moongate.Server.Services.Console;

/// <summary>
/// Implements registration and execution of built-in server commands.
/// </summary>
[RegisterGameEventListener(ServicePriority.CommandSystem)]
public sealed class CommandSystemService : ICommandSystemService, IGameEventListener<CommandEnteredEvent>
{
    private readonly ILogger _logger = Log.ForContext<CommandSystemService>();
    private readonly Dictionary<string, CommandDefinition> _commands = new(StringComparer.OrdinalIgnoreCase);
    private readonly IConsoleUiService _consoleUiService;
    private readonly IGameEventBusService _gameEventBusService;
    private readonly IOutgoingPacketQueue _outgoingPacketQueue;

    public CommandSystemService(
        IConsoleUiService consoleUiService,
        IGameEventBusService gameEventBusService,
        IOutgoingPacketQueue outgoingPacketQueue,
        IServerLifetimeService? _ = null,
        IAccountService? __ = null
    )
    {
        _consoleUiService = consoleUiService;
        _gameEventBusService = gameEventBusService;
        _outgoingPacketQueue = outgoingPacketQueue;
    }

    public async Task ExecuteCommandAsync(
        string commandWithArgs,
        CommandSourceType source = CommandSourceType.Console,
        GameSession? session = null,
        CancellationToken cancellationToken = default
    )
        => await ExecuteInternalAsync(
               commandWithArgs,
               source,
               session,
               (_, level, message, args) => WriteCommandOutput(source, session, level, message, args),
               cancellationToken
           );

    public async Task<IReadOnlyList<string>> ExecuteCommandWithOutputAsync(
        string commandWithArgs,
        CommandSourceType source = CommandSourceType.Console,
        GameSession? session = null,
        CancellationToken cancellationToken = default
    )
    {
        var outputLines = new List<string>();

        await ExecuteInternalAsync(
            commandWithArgs,
            source,
            session,
            (_, _, message, args) =>
            {
                var formatted = args.Length == 0 ? message : string.Format(message, args);
                outputLines.AddRange(
                    formatted.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                );
            },
            cancellationToken
        );

        return outputLines;
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

        var prefix = "";
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

    public IReadOnlyList<CommandDefinition> GetRegisteredCommands()
        => _commands.Values
                    .Distinct()
                    .OrderBy(static definition => definition.Name, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

    public async Task HandleAsync(CommandEnteredEvent gameEvent, CancellationToken cancellationToken = default)
        => await ExecuteCommandAsync(
               gameEvent.CommandText,
               gameEvent.Source,
               gameEvent.GameSession,
               cancellationToken
           );

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

    private async Task ExecuteInternalAsync(
        string commandWithArgs,
        CommandSourceType source,
        GameSession? session,
        Action<CommandSourceType, LogEventLevel, string, object[]> writeOutput,
        CancellationToken cancellationToken
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
            writeOutput(source, LogEventLevel.Warning, "Unknown command: {0}", [commandWithArgs]);

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
            writeOutput(
                source,
                LogEventLevel.Warning,
                "Command '{0}' is not available from source '{1}'.",
                [command, source]
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
            writeOutput(
                source,
                LogEventLevel.Warning,
                "Command '{0}' requires account type '{1}'.",
                [command, commandDefinition.MinimumAccountType]
            );

            return;
        }

        var context = new CommandSystemContext(
            commandWithArgs,
            [.. tokens.Skip(1)],
            source,
            session?.SessionId ?? -1,
            (message, level) => writeOutput(source, level, message, []),
            session?.CharacterId ?? Serial.Zero
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
            writeOutput(
                source,
                LogEventLevel.Error,
                "Command '{0}' failed. Check logs for details.",
                [command]
            );
        }
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
