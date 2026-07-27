using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Commands;
using Moongate.Server.Abstractions.Data.Session;
using Moongate.Server.Abstractions.Types;

namespace Moongate.Server.Abstractions.Interfaces.Commands;

/// <summary>Parses and dispatches a command from any source.</summary>
public interface ICommandService
{
    /// <summary>
    /// In-game adapter: resolves the actor's level from the session's account, strips the leading
    /// "." prefix, and dispatches via <see cref="Execute(CommandInvocation)" /> as
    /// <see cref="CommandSourceType.InGame" />.
    /// </summary>
    void Execute(PlayerSession session, MobileEntity actor, string rawText);

    /// <summary>
    /// Source-neutral core. Unknown names, a command whose <c>Sources</c> excludes
    /// <see cref="CommandInvocation.Source" />, and an actor level below the command's minimum all
    /// produce the identical "Unknown command." reply — a caller cannot tell them apart.
    /// </summary>
    void Execute(CommandInvocation invocation);

    /// <summary>Commands whose <c>Sources</c> include <paramref name="source" />, for help listings.</summary>
    IReadOnlyList<CommandDescriptor> ListCommands(CommandSourceType source);
}
