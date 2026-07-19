using Moongate.Core.Types;

namespace Moongate.Server.Services.Commands;

/// <summary>
/// Default <see cref="Moongate.Server.Abstractions.Interfaces.Commands.ICommandService" />.
/// <see cref="Parse" />/<see cref="IsAuthorized" /> are the pure decision core — public and static
/// so they are unit-testable without a live session, mirroring <c>ChatService.Classify</c>/
/// <c>MovementService.Evaluate</c>.
/// </summary>
public sealed class CommandService
{
    public static bool IsAuthorized(AccountLevelType actorLevel, AccountLevelType minLevel)
        => actorLevel >= minLevel;

    public static (string Name, string[] Arguments) Parse(string rawText)
    {
        var withoutPrefix = rawText[1..]; // strip the leading "."
        var tokens = withoutPrefix.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return tokens.Length == 0 ? (string.Empty, []) : (tokens[0], tokens[1..]);
    }
}
