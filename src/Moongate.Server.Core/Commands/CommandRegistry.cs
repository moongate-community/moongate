using System.Collections.Frozen;
using DryIoc;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Core.Types.Commands;

namespace Moongate.Server.Core.Commands;

/// <summary>Collects command metadata by alias until command system startup freezes registrations.</summary>
public sealed class CommandRegistry
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, CommandRegistration> _registrations = new(StringComparer.OrdinalIgnoreCase);
    private FrozenDictionary<string, CommandRegistration>? _frozen;

    /// <summary>Gets an immutable snapshot keyed by every registered alias.</summary>
    public IReadOnlyDictionary<string, CommandRegistration> Registrations
    {
        get
        {
            lock (_gate)
            {
                return _frozen ?? _registrations.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    /// <summary>Closes registration and returns stable, read-only metadata without resolving executors.</summary>
    public IReadOnlyDictionary<string, CommandRegistration> Freeze()
    {
        lock (_gate)
        {
            return _frozen ??= _registrations.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        }
    }

    internal void Register<TExecutor>(
        Container container,
        string commandName,
        string description,
        CommandSourceType source,
        AccountType minimumAccountType
    )
        where TExecutor : class, ICommandExecutor
    {
        var aliases = ParseAliases(commandName);

        lock (_gate)
        {
            if (_frozen is not null)
            {
                throw new InvalidOperationException("Command registration is frozen.");
            }

            if (aliases.Distinct(StringComparer.OrdinalIgnoreCase).Count() != aliases.Length)
            {
                throw new InvalidOperationException($"Command '{commandName}' repeats an alias.");
            }

            foreach (var alias in aliases)
            {
                if (_registrations.ContainsKey(alias))
                {
                    throw new InvalidOperationException($"A command named '{alias}' is already registered.");
                }
            }

            if (container.IsRegistered<TExecutor>(condition: factory => factory.Reuse != Reuse.Singleton))
            {
                throw new InvalidOperationException(
                    $"Command executor {typeof(TExecutor).Name} must be registered as a singleton."
                );
            }

            if (!container.IsRegistered<TExecutor>())
            {
                container.Register<TExecutor>(Reuse.Singleton);
            }

            var registration = new CommandRegistration(
                new(
                    aliases[0],
                    aliases,
                    description,
                    source,
                    minimumAccountType,
                    typeof(TExecutor)
                ),
                resolver =>
                {
                    var executor = resolver.Resolve<TExecutor>();

                    return context => executor.ExecuteAsync(context);
                }
            );

            foreach (var alias in aliases)
            {
                _registrations.Add(alias, registration);
            }
        }
    }

    private static string[] ParseAliases(string commandName)
    {
        if (string.IsNullOrWhiteSpace(commandName))
        {
            throw new ArgumentException("Command name is required.", nameof(commandName));
        }

        var aliases = commandName
                      .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                      .Select(alias => alias.ToLowerInvariant())
                      .ToArray();

        if (aliases.Length == 0)
        {
            throw new ArgumentException("Command name is required.", nameof(commandName));
        }

        return aliases;
    }
}
