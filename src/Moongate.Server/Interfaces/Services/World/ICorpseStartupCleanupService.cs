using Moongate.Abstractions.Interfaces.Services.Base;

namespace Moongate.Server.Interfaces.Services.World;

/// <summary>
/// Startup hook that removes persisted corpse items before normal runtime begins.
/// </summary>
public interface ICorpseStartupCleanupService : IMoongateService;
