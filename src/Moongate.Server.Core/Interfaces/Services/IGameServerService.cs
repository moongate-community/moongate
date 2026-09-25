namespace Moongate.Server.Core.Interfaces.Services;

/// <summary>
///     Coordinates game sessions and packet dispatch over a separately owned transport service.
/// </summary>
/// <remarks>
///     Stop drains transport and session retirement while packet services and the game loop remain alive.
/// </remarks>
public interface IGameServerService : IMoongateStartupService
{
}
