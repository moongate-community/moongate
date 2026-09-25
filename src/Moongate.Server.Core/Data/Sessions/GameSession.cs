using System.Collections.Concurrent;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Data.Clients;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Accounts;

namespace Moongate.Server.Core.Data.Sessions;

public sealed class GameSession
{
    private readonly IGameLoopService _gameLoop;
    private readonly ConcurrentDictionary<object, object?> _values = new();

    public NetworkSession NetworkSession { get; }

    public long SessionId { get; }

    public Serial AccountId => Get(SessionKeys.AccountId);

    public Serial CharacterId => Get(SessionKeys.CharacterId);

    public AccountType AccountType => Get(SessionKeys.AccountType);

    /// <summary>
    ///     Gets the version the client reported, kept on the connection; <see langword="null" /> until it is known.
    /// </summary>
    public ClientVersion? ClientVersion => NetworkSession.ClientVersion;


    public GameSession(NetworkSession networkSession, IGameLoopService gameLoop)
    {
        ArgumentNullException.ThrowIfNull(networkSession);
        ArgumentNullException.ThrowIfNull(gameLoop);
        NetworkSession = networkSession;
        SessionId = networkSession.SessionId;
        _gameLoop = gameLoop;
    }

    /// <summary>
    ///     Gets the value stored for <paramref name="key" />, or the key's default while nothing has been set.
    ///     Any thread may read.
    /// </summary>
    public T Get<T>(SessionKey<T> key)
    {
        ArgumentNullException.ThrowIfNull(key);

        return _values.TryGetValue(key, out var value) ? (T)value! : key.Default;
    }

    /// <summary>
    ///     Stores <paramref name="value" /> for <paramref name="key" />. Only the game loop thread writes; to clear a
    ///     value, set it back to the key's default.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     Called off the game loop thread.
    /// </exception>
    public void Set<T>(SessionKey<T> key, T value)
    {
        ArgumentNullException.ThrowIfNull(key);
        EnsureLoopThread();

        _values[key] = value;
    }

    private void EnsureLoopThread()
    {
        if (!_gameLoop.IsOnLoopThread)
        {
            throw new InvalidOperationException("Session game state must be changed on the game loop thread.");
        }
    }
}
