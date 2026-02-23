namespace Moongate.Server.Data.Events;

/// <summary>
/// Base payload shared by game events.
/// </summary>
public readonly record struct GameEventBase(long Timestamp) : IGameEvent
{
    /// <summary>
    /// Creates a base event payload with current Unix timestamp in milliseconds.
    /// </summary>
    public static GameEventBase CreateNow()
        => new(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
}
