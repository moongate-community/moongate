using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Data.Commands;

namespace Moongate.Server.Data.Internal.Commands;

/// <summary>
///     Pairs command metadata with the handler resolved for it at startup.
/// </summary>
internal sealed class BoundCommand
{
    public CommandDefinition Definition { get; }

    public Func<CommandContext, Task> Handler { get; }

    /// <summary>
    ///     Gets the executor's argument completion; null when it completes nothing.
    /// </summary>
    public ICommandArgumentCompleter? Completer { get; }

    public BoundCommand(CommandDefinition definition, Func<CommandContext, Task> handler, ICommandArgumentCompleter? completer = null)
    {
        Definition = definition;
        Handler = handler;
        Completer = completer;
    }
}
