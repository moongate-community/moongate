using DryIoc;
using Moongate.Server.Core.Data.Plugins;

namespace Moongate.Server.Core.Interfaces.Plugins;

/// <summary>
///     Declares plugin metadata and registers services before server startup.
/// </summary>
public interface IMoongatePlugin
{
    /// <summary>
    ///     Gets the immutable plugin metadata and required dependencies.
    /// </summary>
    MoongatePluginData Metadata { get; }

    /// <summary>
    ///     Registers services in the host container without starting them.
    /// </summary>
    /// <param name="container">
    ///     The host-owned dependency container.
    /// </param>
    void Register(Container container);
}
