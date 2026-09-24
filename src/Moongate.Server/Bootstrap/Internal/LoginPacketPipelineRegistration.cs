using DryIoc;
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
    public static Container Register(Container container, int listenerPriority = 100)
    {
        container.RegisterDelegate<ILoginNetworkService>(resolver =>
        {
            var config = resolver.Resolve<MoongateServerConfig>();
            return new NetworkService(
                UoNetworkOptionsFactory.CreateLogin(config),
                resolver.Resolve<ILoginConnectionService>());
        }, Reuse.Singleton);
        container.Register<ILoginSessionService, LoginSessionService>(Reuse.Singleton);
        container.RegisterInstance(new LoginPacketHandlerRegistry());
        return container.AddMoongateService<ILoginConnectionService, ConnectionService>(40)
                        .AddMoongateService<ILoginPacketSendService, PacketSendService>(
                            resolver => new PacketSendService(resolver.Resolve<ILoginConnectionService>()), 50)
                        .AddMoongateService<LoginPacketDispatchService>(60)
                        .AddMoongateService<LoginServerService>(resolver => new LoginServerService(
                            resolver.Resolve<ILoginNetworkService>(),
                            resolver.Resolve<ILoginConnectionService>(),
                            resolver.Resolve<ILoginSessionService>(),
                            resolver.Resolve<LoginPacketDispatchService>(),
                            resolver.Resolve<ILoginPacketSendService>()), listenerPriority);
    }
}
