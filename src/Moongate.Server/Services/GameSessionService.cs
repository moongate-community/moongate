using System.Collections.Concurrent;
using Microsoft.Extensions.ObjectPool;
using Moongate.Core.Network.Servers.Tcp;
using Moongate.Core.Server.Interfaces.Services;
using Moongate.UO.Data.Events.GameSessions;
using Moongate.UO.Data.Session;
using Moongate.UO.Interfaces;
using Moongate.UO.Interfaces.Services;
using Serilog;
using ZLinq;

namespace Moongate.Server.Services;

public class GameSessionService : IGameSessionService
{
    public event IGameSessionService.GameSessionCreatedHandler? GameSessionCreated;
    public event IGameSessionService.GameSessionDestroyedHandler? GameSessionDestroyed;
    public event IGameSessionService.GameSessionBeforeDestroyHandler? GameSessionBeforeDestroy;

    private readonly ILogger _logger = Log.ForContext<GameSessionService>();

    private readonly INetworkService _networkService;

    private readonly IEventBusService _eventBusService;

    private readonly ConcurrentDictionary<string, GameSession> _sessions = new();

    private readonly ObjectPool<GameSession> _sessionPool =
        ObjectPool.Create(new DefaultPooledObjectPolicy<GameSession>());

    public GameSessionService(INetworkService networkService, IEventBusService eventBusService)
    {
        _networkService = networkService;
        _eventBusService = eventBusService;
        _networkService.OnClientConnected += OnClientConnected;
        _networkService.OnClientDisconnected += OnClientDisconnected;
    }

    private void OnClientDisconnected(string clientId, MoongateTcpClient client)
    {
        if (_sessions.TryRemove(clientId, out var session))
        {
            GameSessionBeforeDestroy?.Invoke(session);
            _logger.Debug("Client {ClientId} disconnected, removing session.", clientId);
            _eventBusService.PublishAsync(new GameSessionDisconnectedEvent(clientId));
            session.Dispose();
            _sessionPool.Return(session);
            GameSessionDestroyed?.Invoke(session);
        }
    }

    private void OnClientConnected(string clientId, MoongateTcpClient client)
    {
        _logger.Debug("Client {ClientId} connected, adding session.", clientId);
        var newSession = _sessionPool.Get();
        newSession.SessionId = clientId;
        newSession.NetworkClient = client;
        _sessions[clientId] = newSession;

        _eventBusService.PublishAsync(new GameSessionCreatedEvent(clientId, newSession));
        GameSessionCreated?.Invoke(newSession);
    }


    public GameSession? GetSession(string sessionId, bool throwIfNotFound = true)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            return session;
        }

        if (throwIfNotFound)
        {
            throw new KeyNotFoundException($"Session with ID {sessionId} not found.");
        }

        return null;
    }

    public IEnumerable<GameSession> GetSessions()
    {
        return _sessions.Values;
    }

    public IEnumerable<GameSession> QuerySessions(Func<GameSession, bool> predicate)
    {
        return _sessions.Values.AsValueEnumerable().Where(predicate).ToList();
    }

    public GameSession? QuerySessionFirstOrDefault(Func<GameSession, bool> predicate)
    {
        return _sessions.Values.AsValueEnumerable().FirstOrDefault(predicate);
    }

    public void Dispose()
    {
    }
}
