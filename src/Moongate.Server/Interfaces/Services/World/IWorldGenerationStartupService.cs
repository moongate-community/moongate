using Moongate.Abstractions.Interfaces.Services.Base;

namespace Moongate.Server.Interfaces.Services.World;

/// <summary>
/// Startup hook that triggers world generation tasks required for debugging or bootstrap initialization.
/// </summary>
public interface IWorldGenerationStartupService : IMoongateService;
