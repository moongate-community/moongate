using DryIoc;
using Moongate.Server.Ultima.Data.Motd;
using Moongate.Server.Ultima.Interfaces.Motd;

namespace Moongate.Server.Ultima.Extensions;

/// <summary>
///     Adds MOTD variables from a plugin registration callback.
/// </summary>
public static class MotdContainerExtensions
{
    public static Container RegisterMotdVariable(
        this Container container,
        string name,
        Func<MotdContext, CancellationToken, ValueTask<string>> resolver
    )
    {
        ArgumentNullException.ThrowIfNull(container);
        container.Resolve<IMotdVariableRegistry>().Register(name, resolver);

        return container;
    }
}
