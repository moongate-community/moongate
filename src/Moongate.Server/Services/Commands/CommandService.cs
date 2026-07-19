using System.Reflection;
using Moongate.Core.Types;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Attributes;
using Moongate.Server.Abstractions.Data.Commands;
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
/// actor's level, checks authorization, dispatches, replies.
/// </summary>
public sealed class CommandService : ICommandService
{
    private const string UnknownCommandMessage = "Unknown command.";
    private const string CommandFailedMessage = "Command failed. Check server logs.";

    private readonly ILogger _logger = Log.ForContext<CommandService>();
    private readonly Dictionary<string, (ICommand Command, CommandAttribute Attribute)> _registry;
    private readonly IAccountService _accounts;

    public CommandService(IEnumerable<ICommand> commands, IAccountService accounts)
    {
        _registry = BuildRegistry(commands);
        _accounts = accounts;
    }

    /// <summary>
    /// Reads each command's <see cref="CommandAttribute" /> once and indexes it under every
    /// pipe-delimited alias in <see cref="CommandAttribute.Name" />, case-insensitively. Pure aside
    /// from reflection (no I/O), so it is unit-testable without any session/account infrastructure.
    /// </summary>
    public static Dictionary<string, (ICommand Command, CommandAttribute Attribute)> BuildRegistry(
        IEnumerable<ICommand> commands
    )
    {
        var registry = new Dictionary<string, (ICommand, CommandAttribute)>(StringComparer.OrdinalIgnoreCase);

        foreach (var command in commands)
        {
            var attribute = command.GetType().GetCustomAttribute<CommandAttribute>()
                          ?? throw new InvalidOperationException(
                                 $"{command.GetType().Name} is missing [Command]."
                             );

            foreach (var alias in attribute.Name.Split('|'))
            {
                registry[alias] = (command, attribute);
            }
        }

        return registry;
    }

    public static bool IsAuthorized(AccountLevelType actorLevel, AccountLevelType minLevel)
        => actorLevel >= minLevel;

    public static (string Name, string[] Arguments) Parse(string rawText)
    {
        var withoutPrefix = rawText[1..]; // strip the leading "."
        var tokens = withoutPrefix.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return tokens.Length == 0 ? (string.Empty, []) : (tokens[0], tokens[1..]);
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

        if (!IsAuthorized(actorLevel, registration.Attribute.MinLevel))
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
            registration.Command.Execute(context);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Command '{Name}' failed", name);
            session.Send(ChatMessageFactory.CreateSystem(CommandFailedMessage));
        }
    }
}
