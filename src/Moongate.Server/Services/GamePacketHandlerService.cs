using DryIoc;
using Moongate.Core.Server.Interfaces.Packets;
using Moongate.Core.Server.Interfaces.Services;
using Moongate.UO.Interfaces.Handlers;
using Moongate.UO.Interfaces.Services;
using Serilog;

namespace Moongate.Server.Services;

public class GamePacketHandlerService : IGamePacketHandlerService
{
    private readonly ILogger _logger = Log.ForContext<GamePacketHandlerService>();

    private readonly INetworkService _networkService;
    private readonly IGameSessionService _gameSessionService;
    private readonly IContainer _container;

    private readonly Dictionary<Type, List<IGamePacketHandler>> _packetHandlers = new();


    public GamePacketHandlerService(
        INetworkService networkService, IContainer container, IGameSessionService gameSessionService
    )
    {
        _networkService = networkService;
        _container = container;
        _gameSessionService = gameSessionService;
    }

    public void RegisterGamePacketHandler<TPacket, TGamePacketHandler>() where TPacket : IUoNetworkPacket, new()
        where TGamePacketHandler : IGamePacketHandler
    {
        if (!_container.IsRegistered<TGamePacketHandler>())
        {
            _logger.Verbose(
                "Registering game packet handler for {PacketType} with handler {HandlerType}",
                typeof(TPacket).Name,
                typeof(TGamePacketHandler).Name
            );
            _container.Register<TGamePacketHandler>(Reuse.Singleton);
        }

        if (!_networkService.IsPacketBound<TPacket>())
        {
            _logger.Verbose(
                "Binding packet {PacketType} to network service",
                typeof(TPacket).Name
            );
            _networkService.BindPacket<TPacket>();
        }


        if (!_packetHandlers.TryGetValue(typeof(TPacket), out var handlers))
        {
            _packetHandlers[typeof(TPacket)] = [];
        }

        _packetHandlers[typeof(TPacket)].Add(_container.Resolve<TGamePacketHandler>());


        _networkService.RegisterPacketHandler<TPacket>(OnPacketReceived);
    }

    private async Task OnPacketReceived(string sessionId, IUoNetworkPacket packet)
    {
        foreach (var handler in _packetHandlers[packet.GetType()])
        {
            try
            {
                var gameSession = _gameSessionService.GetSession(sessionId);

                await handler.HandlePacketAsync(gameSession, packet);
            }
            catch (Exception ex)
            {
                _logger.Error(
                    ex,
                    "Error handling packet {PacketType} with handler {HandlerType}",
                    packet.GetType().Name,
                    handler.GetType().Name
                );

                throw;
            }
        }
    }

    public void Dispose()
    {
    }
}
