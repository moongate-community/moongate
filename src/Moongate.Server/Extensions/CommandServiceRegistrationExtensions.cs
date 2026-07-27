using DryIoc;
using Moongate.Server.Abstractions.Data.Internal;
using Moongate.Server.Abstractions.Interfaces.Commands;
using Moongate.Server.Services.Commands;

namespace Moongate.Server.Extensions;

/// <summary>
/// Wires the "." command dispatcher: an empty-safe read-only view over the recorded
/// <see cref="CommandRegistration" />s plus the <see cref="ICommandService" /> implementation. The
/// per-command seam (<c>RegisterCommand</c>) lives in Moongate.Server.Abstractions so plugins can use
/// it; wiring the concrete service is the composition root's job and stays here — mirrors
/// <c>RegisterDataLoaderService</c>.
/// </summary>
public static class CommandServiceRegistrationExtensions
{
    /// <summary>
    /// Registers the read-only registration list (defaulting to empty when no command is registered)
    /// and the <see cref="ICommandService" /> as a singleton.
    /// </summary>
    public static IContainer RegisterCommandService(this IContainer container)
    {
        container.RegisterDelegate<IReadOnlyList<CommandRegistration>>(
            static resolver => resolver.Resolve<List<CommandRegistration>>(IfUnresolved.ReturnDefault) ?? [],
            Reuse.Singleton
        );

        container.Register<ICommandService, CommandService>(Reuse.Singleton);

        return container;
    }
}
