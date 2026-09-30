using DryIoc;
using Moongate.Server.Ultima.Interfaces.Motd;
using Moongate.Server.Ultima.Interfaces.Titles;
using Moongate.Server.Ultima.Services.Motd;
using Moongate.Server.Ultima.Services.Titles;

namespace Moongate.Server.Ultima.Extensions;

/// <summary>
///     Registers the message of the day and the fame and karma titles shown in game.
/// </summary>
public static class UltimaMotdContainerExtensions
{
    /// <summary>
    ///     Registers the message of the day and the fame and karma titles shown in game.
    /// </summary>
    public static Container AddUltimaMotd(this Container container)
    {
        container.Register<IMotdVariableRegistry, MotdVariableRegistry>(Reuse.Singleton);
        MotdRenderer.RegisterBuiltins(container.Resolve<IMotdVariableRegistry>());
        container.Register<MotdRenderer>(Reuse.Singleton);
        container.Register<IMotdService, MotdService>(Reuse.Singleton);
        container.Register<IFameKarmaTitleService, FameKarmaTitleService>(Reuse.Singleton);

        return container;
    }
}
