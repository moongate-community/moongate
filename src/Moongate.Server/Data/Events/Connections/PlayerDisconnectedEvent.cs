using Moongate.Server.Data.Events.Base;

namespace Moongate.Server.Data.Events.Connections;

/// <summary>
/// Event emitted when a client session disconnects from the server.
/// </summary>
public readonly record struct PlayerDisconnectedEvent(
    GameEventBase BaseEvent,
    long SessionId,
    string? RemoteEndPoint
) : IGameEvent
{
    /// <summary>
    /// Creates a disconnected event with current timestamp.
    /// </summary>
    public PlayerDisconnectedEvent(long sessionId, string? remoteEndPoint)
        : this(GameEventBase.CreateNow(), sessionId, remoteEndPoint) { }

    /// <summary>
    /// Creates a disconnected event with explicit timestamp.
    /// </summary>
    public PlayerDisconnectedEvent(long sessionId, string? remoteEndPoint, long timestamp)
        : this(new(timestamp), sessionId, remoteEndPoint) { }
}
