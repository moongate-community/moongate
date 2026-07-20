using System.Linq;
using DryIoc;
using Moongate.Core.Types;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Commands;
using Moongate.Server.Abstractions.Data.Internal;
using Moongate.Server.Abstractions.Data.Session;
using Moongate.Server.Abstractions.Interfaces.Accounts;
using Moongate.Server.Abstractions.Interfaces.Commands;
using Moongate.Server.Abstractions.Types;
using Moongate.Server.Services.Chat;
using Serilog;

namespace Moongate.Server.Services.Commands;

/// <summary>
/// Default <see cref="ICommandService" />. <see cref="Parse" />/<see cref="IsAuthorized" />/
/// <see cref="BuildRegistry" /> are the pure decision core — public and static so they are
/// unit-testable without a live session, mirroring <c>ChatService.Classify</c>/
/// <c>MovementService.Evaluate</c>. <c>Execute(CommandInvocation)</c> is the impure core — it gates
/// by source and level, then resolves and dispatches; the session overload is the in-game adapter.
/// </summary>
public sealed class CommandService : ICommandService
{
    private const string UnknownCommandMessage = "Unknown command.";
    private const string CommandFailedMessage = "Command failed. Check server logs.";

    private readonly ILogger _logger = Log.ForContext<CommandService>();
    private readonly Dictionary<string, CommandRegistration> _registry;
    private readonly IResolverContext _resolver;
    private readonly IAccountService _accounts;

    public CommandService(IReadOnlyList<CommandRegistration> registrations, IResolverContext resolver, IAccountService accounts)
    {
        _registry = BuildRegistry(registrations);
        _resolver = resolver;
        _accounts = accounts;
    }

    /// <summary>
    /// Indexes each registration under every pipe-delimited alias in
    /// <see cref="CommandRegistration.Name" />, case-insensitively. Pure (no reflection, no I/O), so it
    /// is unit-testable without any session/account/container infrastructure.
    /// </summary>
    public static Dictionary<string, CommandRegistration> BuildRegistry(IEnumerable<CommandRegistration> registrations)
    {
        var registry = new Dictionary<string, CommandRegistration>(StringComparer.OrdinalIgnoreCase);

        foreach (var registration in registrations)
        {
            foreach (var alias in registration.Name.Split('|'))
            {
                registry[alias] = registration;
            }
        }

        return registry;
    }

    public void Execute(PlayerSession session, MobileEntity actor, string rawText)
    {
        var commandLine = rawText.StartsWith('.') ? rawText[1..] : rawText;
        var actorLevel = _accounts.GetById(session.AccountId)?.AccountLevel ?? AccountLevelType.Player;

        Execute(
            new CommandInvocation(
                CommandSourceType.InGame,
                actorLevel,
                actor,
                commandLine,
                message => session.Send(ChatMessageFactory.CreateSystem(message))
            )
        );
    }

    public void Execute(CommandInvocation invocation)
    {
        var (name, arguments) = Parse(invocation.CommandLine);

        if (name.Length == 0
            || !_registry.TryGetValue(name, out var registration)
            || !registration.Sources.HasFlag(invocation.Source)
            || !IsAuthorized(invocation.ActorLevel, registration.MinLevel))
        {
            invocation.Reply(UnknownCommandMessage);

            return;
        }

        var context = new CommandContext(invocation.Source, invocation.Actor, arguments, invocation.Reply);

        try
        {
            registration.Resolve(_resolver).Execute(context);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Command '{Name}' failed", name);
            invocation.Reply(CommandFailedMessage);
        }
    }

    public IReadOnlyList<CommandDescriptor> ListCommands(CommandSourceType source)
        => _registry.Values
                    .Where(registration => registration.Sources.HasFlag(source))
                    .Distinct()
                    .Select(
                        registration => new CommandDescriptor(
                            registration.Name.Split('|')[0],
                            registration.MinLevel,
                            registration.Description
                        )
                    )
                    .ToList();

    public static bool IsAuthorized(AccountLevelType actorLevel, AccountLevelType minLevel)
        => actorLevel >= minLevel;

    public static (string Name, string[] Arguments) Parse(string commandLine)
    {
        var tokens = commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return tokens.Length == 0 ? (string.Empty, []) : (tokens[0], tokens[1..]);
    }
}
