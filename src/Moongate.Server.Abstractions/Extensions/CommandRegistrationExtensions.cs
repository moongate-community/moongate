using DryIoc;
using Moongate.Core.Types;
using Moongate.Server.Abstractions.Data.Internal;
using Moongate.Server.Abstractions.Interfaces.Commands;
using Moongate.Server.Abstractions.Types;
using SquidStd.Abstractions.Extensions.Container;

namespace Moongate.Server.Abstractions.Extensions;

/// <summary>
/// Registers GM/admin commands declaratively: the metadata (name/aliases, minimum level, help text,
/// sources) is stated here and collected in a typed <see cref="CommandRegistration" /> list the
/// command service reads at startup — mirrors <c>RegisterDataLoader</c>, no attribute reflection.
/// </summary>
public static class CommandRegistrationExtensions
{
    /// <summary>
    /// Records <typeparamref name="TCommand" /> as a singleton and adds its
    /// <see cref="CommandRegistration" /> to the typed list. <paramref name="name" /> may be
    /// pipe-delimited ("broadcast|bc") to register aliases; the first token is the canonical name.
    /// </summary>
    public static IContainer RegisterCommand<TCommand>(
        this IContainer container,
        string name,
        AccountLevelType minLevel,
        string description,
        CommandSourceType sources = CommandSourceType.InGame
    )
        where TCommand : class, ICommand
    {
        container.Register<TCommand>(Reuse.Singleton);
        container.AddToRegisterTypedList(
            new CommandRegistration(name, minLevel, description, sources, static resolver => resolver.Resolve<TCommand>())
        );

        return container;
    }
}
