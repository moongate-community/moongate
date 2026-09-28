using Moongate.Server.Core.Data.Sessions;

namespace Moongate.Server.Core.Interfaces.Sessions;

/// <summary>
///     Told when a game session closes, so a service can let go of what the session held, such as its character in the
///     world.
/// </summary>
/// <remarks>
///     Called on the game loop while the session is still registered, before it is removed. Do only in-memory work
///     here and start anything slow, such as a save, as a task of your own. An exception is logged and does not stop
///     the session from closing.
/// </remarks>
public interface ISessionClosedListener
{
    void OnSessionClosed(GameSession session);
}
