using DryIoc;
using Moongate.Server.Abstractions.Interfaces.Commands;

namespace Moongate.Server.Abstractions.Extensions;

/// <summary>
/// Registers GM/admin commands. Each command is collected under <see cref="ICommand" />, the typed
/// <see cref="System.Collections.Generic.IEnumerable{T}" /> that <c>CommandService</c> resolves and
/// indexes by name/alias at construction — mirrors <c>RegisterPacketHandler</c>'s
/// <see cref="Interfaces.Network.IPacketHandlerRegistration" /> collection pattern.
/// </summary>
public static class CommandRegistrationExtensions
{
    public static IContainer RegisterCommand<TCommand>(this IContainer container)
        where TCommand : class, ICommand
    {
        container.Register<ICommand, TCommand>(Reuse.Singleton);

        return container;
    }
}
