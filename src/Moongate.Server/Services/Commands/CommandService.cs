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
/// <c>MovementService.Evaluate</c>. <see cref="Execute" /> is the impure orchestrator: resolves the
/// actor's level, checks authorization, resolves and dispatches the command, replies.
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
        var (name, arguments) = Parse(rawText);

        if (name.Length == 0 || !_registry.TryGetValue(name, out var registration))
        {
            session.Send(ChatMessageFactory.CreateSystem(UnknownCommandMessage));

            return;
        }

        var actorLevel = _accounts.GetById(session.AccountId)?.AccountLevel ?? AccountLevelType.Player;

        if (!IsAuthorized(actorLevel, registration.MinLevel))
        {
            session.Send(ChatMessageFactory.CreateSystem(UnknownCommandMessage));

            return;
        }

        var context = new CommandContext(
            CommandSourceType.InGame,
            actor,
            arguments,
            message => session.Send(ChatMessageFactory.CreateSystem(message))
        );

        try
        {
            registration.Resolve(_resolver).Execute(context);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Command '{Name}' failed", name);
            session.Send(ChatMessageFactory.CreateSystem(CommandFailedMessage));
        }
    }

    public static bool IsAuthorized(AccountLevelType actorLevel, AccountLevelType minLevel)
        => actorLevel >= minLevel;

    public static (string Name, string[] Arguments) Parse(string rawText)
    {
        var withoutPrefix = rawText[1..]; // strip the leading "."
        var tokens = withoutPrefix.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return tokens.Length == 0 ? (string.Empty, []) : (tokens[0], tokens[1..]);
    }
}
