using DryIoc;
using Moongate.Network.Packets.Incoming.Login;
using Moongate.Server.Core.Data.Plugins;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Tests.TestSupport.Packets;

public sealed class DeferredPacketPluginLoader : IPluginLoaderService
{
    private readonly Container _container;
    public IReadOnlyList<MoongatePluginData> Plugins => [];

    public DeferredPacketPluginLoader(Container container)
    {
        _container = container;
    }

    public void LoadPlugins()
    {
        _container.AddMoongateService<PacketPluginDependency>();
        _container.RegisterPacketHandler<ServerSelectPacket, PluginServerSelectHandler>();
    }
}
