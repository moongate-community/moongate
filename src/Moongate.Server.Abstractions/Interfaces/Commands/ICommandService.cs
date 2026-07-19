using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Session;

namespace Moongate.Server.Abstractions.Interfaces.Commands;

/// <summary>
/// Dispatches a "." command typed by <paramref name="actor" /> in <paramref name="session" />.
/// Unknown command names and commands the actor's <see cref="Moongate.Core.Types.AccountLevelType" />
/// does not meet produce the identical "Unknown command." reply — an unauthorized caller cannot
/// distinguish "doesn't exist" from "you can't use this".
/// </summary>
public interface ICommandService
{
    void Execute(PlayerSession session, MobileEntity actor, string rawText);
}
