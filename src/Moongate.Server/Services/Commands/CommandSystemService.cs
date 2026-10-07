using System.Collections.Frozen;
using DryIoc;
using Moongate.Server.Core.Commands;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Data.Internal.Commands;
using Serilog;

namespace Moongate.Server.Services.Commands;

/// <summary>
///     Dispatches registered commands on the calling thread and collects their output.
/// </summary>
public sealed class CommandSystemService : ICommandSystemService
{
    private readonly Lock _gate = new();
    private readonly CommandRegistry _registry;
    private readonly IResolverContext _resolver;
    private readonly ILogger _logger;

    private FrozenDictionary<string, BoundCommand> _commands = FrozenDictionary<string, BoundCommand>.Empty;
    private bool _running;
    private bool _stopped;

    public CommandSystemService(CommandRegistry registry, IResolverContext resolver)
        : this(registry, resolver, Log.ForContext<CommandSystemService>())
    {
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CommandOutputLine>> ExecuteAsync(
        string commandLine,
        CommandSourceType source = CommandSourceType.Console,
        GameSession? session = null,
        CancellationToken cancellationToken = default
    )
    {
        FrozenDictionary<string, BoundCommand> commands;

        lock (_gate)
        {
            if (!_running)
            {
                throw new InvalidOperationException("The command system is not running.");
            }

            commands = _commands;
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return [];
        }

        var tokens = commandLine.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
        );
        var name = tokens[0].ToLowerInvariant();
        var context = new CommandContext(commandLine, name, tokens[1..], source, session, cancellationToken);
        // Absent on the login role: the texts are then the English ones.
        var localization = _resolver.Resolve<ILocalizationService>(IfUnresolved.ReturnDefault);

        if (!commands.TryGetValue(name, out var command))
        {
            _logger.Verbose("An unregistered command was requested");
            context.PrintError(localization.Text(CommandMessages.UnknownCommand, "Unknown command: {0}", name));

            return context.Output;
        }

        if (source == CommandSourceType.None || !command.Definition.Source.HasFlag(source))
        {
            context.PrintError(
                localization.Text(CommandMessages.NotAvailableHere, "The command '{0}' is not available here.", name)
            );

            return context.Output;
        }

        if (ResolveInvokerAccountType(source, session) < command.Definition.MinimumAccountType)
        {
            context.PrintError(
                localization.Text(CommandMessages.NotAllowed, "You are not allowed to use the command '{0}'.", name)
            );

            return context.Output;
        }

        try
        {
            await command.Handler(context);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "Command '{Command}' execution failed", name);
            context.PrintError(localization.Text(CommandMessages.CommandFailed, "The command '{0}' failed.", name));
        }

        return context.Output;
    }

    /// <inheritdoc />
    public IReadOnlyList<CommandDefinition> GetRegisteredCommands()
    {
        FrozenDictionary<string, BoundCommand> commands;

        lock (_gate)
        {
            commands = _commands;
        }

        return commands.Values
            .Select(command => command.Definition)
            .Distinct()
            .OrderBy(definition => definition.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <inheritdoc />
    public Task StartAsync()
    {
        lock (_gate)
        {
            if (_stopped)
            {
                throw new InvalidOperationException("The command system cannot restart after shutdown.");
            }

            if (!_running)
            {
                _commands = BindCommands();
                _running = true;
                _logger.Information(
                    "Command system started with {AliasCount} command aliases.",
                    _commands.Count
                );
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync()
    {
        lock (_gate)
        {
            _running = false;
            _stopped = true;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetArgumentCompletions(
        string commandName,
        IReadOnlyList<string> previousArguments,
        CommandSourceType source = CommandSourceType.Console
    )
    {
        FrozenDictionary<string, BoundCommand> commands;

        lock (_gate)
        {
            commands = _commands;
        }

        if (!commands.TryGetValue(commandName, out var command) ||
            source == CommandSourceType.None ||
            !command.Definition.Source.HasFlag(source) ||
            command.Completer is null)
        {
            return [];
        }

        try
        {
            // A value the parser would split, or none at all, cannot be typed: a careless plugin must not reach the console.
            return command.Completer.GetArgumentCompletions(previousArguments)
                       ?.Where(value => !string.IsNullOrEmpty(value) && !value.Any(char.IsWhiteSpace))
                       .ToArray() ??
                   [];
        }
        catch (Exception exception)
        {
            _logger.Warning(exception, "Completing the arguments of '{Command}' failed", commandName);

            return [];
        }
    }

    internal CommandSystemService(CommandRegistry registry, IResolverContext resolver, ILogger logger)
    {
        _registry = registry;
        _resolver = resolver;
        _logger = logger;
    }

    private FrozenDictionary<string, BoundCommand> BindCommands()
    {
        var handlers = new Dictionary<CommandRegistration, Func<CommandContext, Task>>();
        var completers = new Dictionary<CommandRegistration, ICommandArgumentCompleter?>();
        var commands = new Dictionary<string, BoundCommand>(StringComparer.OrdinalIgnoreCase);

        foreach (var (alias, registration) in _registry.Freeze())
        {
            if (!handlers.TryGetValue(registration, out var handler))
            {
                handler = registration.Bind(_resolver);
                handlers.Add(registration, handler);

                // The executors are singletons: this is the instance the handler runs.
                var executor = _resolver.Resolve(registration.Definition.ExecutorType);
                completers.Add(registration, executor as ICommandArgumentCompleter);
            }

            commands.Add(alias, new(registration.Definition, handler, completers[registration]));
        }

        return commands.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
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
}
