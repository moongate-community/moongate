using DryIoc;
using Moongate.Core.Server.Instances;
using Moongate.Core.Server.Interfaces.Packets;
using Moongate.Core.Server.Interfaces.Services;
using Moongate.UO.Interfaces.Handlers;
using Moongate.UO.Interfaces.Services;
using Serilog;

namespace Moongate.UO.Extensions;

public static class GamePacketHandlerExtension
{
    private static readonly ILogger _logger = Log.ForContext(typeof(GamePacketHandlerExtension));

    public static void RegisterGamePacketHandler<TPacket, TGamePacketHandler>(this INetworkService networkService)
        where TPacket : IUoNetworkPacket, new()
        where TGamePacketHandler : IGamePacketHandler
    {
        _logger.Debug(
            "Registering game packet handler for {PacketType} with handler {HandlerType}",
            typeof(TPacket).Name,
            typeof(TGamePacketHandler).Name
        );


        if (!networkService.IsPacketBound<TPacket>())
        {
            networkService.BindPacket<TPacket>();
        }

        if (!MoongateContext.Container.IsRegistered<TGamePacketHandler>())
        {
            MoongateContext.Container.Register<TGamePacketHandler>(Reuse.Singleton);
        }

        networkService.RegisterPacketHandler<TPacket>((id, packet) =>
            {
                var gameSessionService = MoongateContext.Container.Resolve<IGameSessionService>();
                var gamePacketHandler = MoongateContext.Container.Resolve<TGamePacketHandler>();

                var gameSession = gameSessionService.GetSession(id);

                return gamePacketHandler.HandlePacketAsync(gameSession, packet);
            }
        );
    }
}
