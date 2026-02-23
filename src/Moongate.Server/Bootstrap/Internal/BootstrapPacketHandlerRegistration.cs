using DryIoc;
using Moongate.Server.Interfaces.Listener;
using Moongate.Server.Interfaces.Services.Packets;

namespace Moongate.Server.Bootstrap.Internal;

/// <summary>
/// Registers packet listeners for built-in packet opcodes.
/// </summary>
internal static partial class BootstrapPacketHandlerRegistration
{
    public static void Register(Container container)
    {
        RegisterGenerated(container);
    }

    static partial void RegisterGenerated(Container container);

    private static void RegisterPacketHandler<T>(Container container, byte opCode) where T : IPacketListener
    {
        if (!container.IsRegistered<T>())
        {
            container.Register<T>();
        }

        var handler = container.Resolve<T>();
        var packetListenerService = container.Resolve<IPacketDispatchService>();
        packetListenerService.AddPacketListener(opCode, handler);
    }
}
