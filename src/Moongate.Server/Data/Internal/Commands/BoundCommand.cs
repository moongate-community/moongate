using Moongate.Server.Core.Data.Commands;

namespace Moongate.Server.Data.Internal.Commands;

/// <summary>
///     Pairs command metadata with the handler resolved for it at startup.
/// </summary>
internal sealed class BoundCommand
{
    public CommandDefinition Definition { get; }

    public Func<CommandContext, Task> Handler { get; }

    public BoundCommand(CommandDefinition definition, Func<CommandContext, Task> handler)
    {
        Definition = definition;
        Handler = handler;
    }
}
