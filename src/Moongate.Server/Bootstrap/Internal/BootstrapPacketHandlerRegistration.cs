using DryIoc;
using Moongate.Server.Attributes;
using Moongate.Server.Interfaces.Listener;
using Moongate.Server.Interfaces.Services.Packets;

namespace Moongate.Server.Bootstrap.Internal;

/// <summary>
/// Registers packet listeners for built-in packet opcodes.
/// </summary>
internal static partial class BootstrapPacketHandlerRegistration
{
    public static void Register(Container container)
        => RegisterGenerated(container);

    public static void Register(Container container, IEnumerable<Type> pluginHandlerTypes)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(pluginHandlerTypes);

        RegisterGenerated(container);

        foreach (var handlerType in pluginHandlerTypes)
        {
            RegisterPacketHandler(container, handlerType);
        }
    }

    static partial void RegisterGenerated(Container container);

    private static void RegisterPacketHandler<T>(Container container, byte opCode) where T : IPacketListener
    {
        if (!container.IsRegistered<T>())
        {
            container.Register<T>(Reuse.Singleton);
        }

        var handler = container.Resolve<T>();
        var packetListenerService = container.Resolve<IPacketDispatchService>();
        packetListenerService.AddPacketListener(opCode, handler);
    }

    private static void RegisterPacketHandler(Container container, Type handlerType)
    {
        if (!typeof(IPacketListener).IsAssignableFrom(handlerType))
        {
            throw new InvalidOperationException($"Type '{handlerType.FullName}' does not implement IPacketListener.");
        }

        if (!container.IsRegistered(handlerType))
        {
            container.Register(handlerType, Reuse.Singleton);
        }

        var handler = (IPacketListener)container.Resolve(handlerType);
        var packetListenerService = container.Resolve<IPacketDispatchService>();

        foreach (var attribute in handlerType.GetCustomAttributes(typeof(RegisterPacketHandlerAttribute), false)
                                             .Cast<RegisterPacketHandlerAttribute>())
        {
            packetListenerService.AddPacketListener(attribute.OpCode, handler);
        }
    }
}
