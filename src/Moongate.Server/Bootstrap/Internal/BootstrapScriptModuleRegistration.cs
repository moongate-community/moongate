using DryIoc;

namespace Moongate.Server.Bootstrap.Internal;

/// <summary>
/// Registers script modules discovered by source generation.
/// </summary>
internal static partial class BootstrapScriptModuleRegistration
{
    public static void Register(Container container)
    {
        RegisterGenerated(container);
    }

    static partial void RegisterGenerated(Container container);
}
