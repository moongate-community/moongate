using System.Collections.Frozen;
using DryIoc;
using Moongate.Server.Core.Commands;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Data.Internal.Commands;
using Serilog;

namespace Moongate.Server.Services.Commands;

/// <summary>Dispatches registered commands on the calling thread and collects their output.</summary>
public sealed class CommandSystemService : ICommandSystemService
{
    private readonly Lock _gate = new();
    private readonly CommandRegistry _registry;
    private readonly IResolverContext _resolver;
    private readonly ILogger _logger = Log.ForContext<CommandSystemService>();

    private FrozenDictionary<string, BoundCommand> _commands = FrozenDictionary<string, BoundCommand>.Empty;
    private bool _running;
    private bool _stopped;

    public CommandSystemService(CommandRegistry registry, IResolverContext resolver)
    {
        _registry = registry;
        _resolver = resolver;
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

        var tokens = commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var name = tokens[0].ToLowerInvariant();
        var context = new CommandContext(commandLine, name, tokens[1..], source, session, cancellationToken);

        if (!commands.TryGetValue(name, out var command))
        {
            _logger.Verbose("Command '{Command}' is not registered", name);
            context.PrintError("Unknown command: {0}", name);

            return context.Output;
        }

        if (source == CommandSourceType.None || !command.Definition.Source.HasFlag(source))
        {
            context.PrintError("Command '{0}' is not available from source '{1}'.", name, source);

            return context.Output;
        }

        if (ResolveInvokerAccountType(source, session) < command.Definition.MinimumAccountType)
        {
            context.PrintError("Command '{0}' requires account type '{1}'.", name, command.Definition.MinimumAccountType);

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
            context.PrintError("Command '{0}' failed. Check logs for details.", name);
        }

        return context.Output;
    }

    private FrozenDictionary<string, BoundCommand> BindCommands()
    {
        var handlers = new Dictionary<CommandRegistration, Func<CommandContext, Task>>();
        var commands = new Dictionary<string, BoundCommand>(StringComparer.OrdinalIgnoreCase);

        foreach (var (alias, registration) in _registry.Freeze())
        {
            if (!handlers.TryGetValue(registration, out var handler))
            {
                handler = registration.Bind(_resolver);
                handlers.Add(registration, handler);
            }

            commands.Add(alias, new BoundCommand(registration.Definition, handler));
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
