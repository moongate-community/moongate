using Moongate.Server.Data.Events.Base;
using Moongate.Server.Attributes;

namespace Moongate.Server.Data.Events.Connections;

/// <summary>
/// Event emitted when a new client session connects to the server.
/// </summary>
[RegisterLuaUserData]
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
        : this(new(timestamp), sessionId, remoteEndPoint) { }
}
