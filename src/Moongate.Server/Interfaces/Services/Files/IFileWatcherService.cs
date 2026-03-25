using Moongate.Abstractions.Interfaces.Services.Base;

namespace Moongate.Server.Interfaces.Services.Files;

/// <summary>
/// Watches runtime files and posts hot-reload work back to the game loop.
/// </summary>
public interface IFileWatcherService : IMoongateService;
