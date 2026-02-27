using DryIoc;

namespace Moongate.Server.Bootstrap.Internal;

/// <summary>
/// Registers Lua user-data types discovered via source generation.
/// </summary>
internal static partial class BootstrapLuaUserDataRegistration
{
    public static void Register(IContainer container)
        => RegisterGenerated(container);

    static partial void RegisterGenerated(IContainer container);
}
