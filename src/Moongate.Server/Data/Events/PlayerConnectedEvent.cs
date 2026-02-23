namespace Moongate.Server.Data.Events;

/// <summary>
/// Event emitted when a new client session connects to the server.
/// </summary>
public readonly record struct PlayerConnectedEvent(
    GameEventBase BaseEvent,
    long SessionId,
    string? RemoteEndPoint
) : IGameEvent
{
    /// <summary>
    /// Creates a connected event with current timestamp.
    /// </summary>
    public PlayerConnectedEvent(long sessionId, string? remoteEndPoint)
        : this(GameEventBase.CreateNow(), sessionId, remoteEndPoint) { }

    /// <summary>
    /// Creates a connected event with explicit timestamp.
    /// </summary>
    public PlayerConnectedEvent(long sessionId, string? remoteEndPoint, long timestamp)
        : this(new GameEventBase(timestamp), sessionId, remoteEndPoint) { }
}
