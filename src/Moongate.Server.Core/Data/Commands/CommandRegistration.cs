using DryIoc;

namespace Moongate.Server.Core.Data.Commands;

/// <summary>Immutable command metadata with a binder invoked once by command system startup.</summary>
public sealed class CommandRegistration
{
    /// <summary>Gets the command metadata shared by every alias.</summary>
    public CommandDefinition Definition { get; }

    /// <summary>Gets the deferred binder that resolves the executor and returns its handler.</summary>
    public Func<IResolverContext, Func<CommandContext, Task>> Bind { get; }

    public CommandRegistration(
        CommandDefinition definition,
        Func<IResolverContext, Func<CommandContext, Task>> bind
    )
    {
        Definition = definition;
        Bind = bind;
    }
}
