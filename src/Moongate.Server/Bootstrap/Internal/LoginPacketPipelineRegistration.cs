using DryIoc;
using Moongate.Server.Core.Data.Network;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Packets;
using Moongate.Server.Data.Config;
using Moongate.Server.Services.Login;
using Moongate.Server.Services.Network;
using Moongate.Server.Services.Packets;

namespace Moongate.Server.Bootstrap.Internal;

internal static class LoginPacketPipelineRegistration
{
    public static Container Register(Container container)
    {
        container.RegisterDelegate<NetworkListenerOptions>(
            resolver => GameNetworkOptionsFactory.Create(resolver.Resolve<MoongateServerConfig>()), Reuse.Singleton);
        container.Register<INetworkService, NetworkService>(Reuse.Singleton);
        container.Register<ILoginSessionService, LoginSessionService>(Reuse.Singleton);
        container.RegisterInstance(new LoginPacketHandlerRegistry());
        return container.AddMoongateService<IConnectionService, ConnectionService>(40)
                        .AddMoongateService<IPacketSendService, PacketSendService>(50)
                        .AddMoongateService<LoginPacketDispatchService>(60)
                        .AddMoongateService<LoginServerService>(100);
    }
}
