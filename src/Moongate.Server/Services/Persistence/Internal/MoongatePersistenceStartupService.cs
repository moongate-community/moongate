using Moongate.Persistence.Services;
using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Server.Services.Persistence.Internal;

internal sealed class MoongatePersistenceStartupService : IMoongateStartupService
{
    internal const int StartupPriority = -1000;

    private readonly MoongatePersistenceService _persistence;

    public MoongatePersistenceStartupService(MoongatePersistenceService persistence)
    {
        _persistence = persistence;
    }

    public Task StartAsync()
    {
        return _persistence.InitializeAsync();
    }

    public Task StopAsync()
    {
        return _persistence.DisposeAsync().AsTask();
    }
}
