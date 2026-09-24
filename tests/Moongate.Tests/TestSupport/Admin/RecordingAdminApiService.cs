using Moongate.Server.Core.Interfaces.Admin;

namespace Moongate.Tests.TestSupport.Admin;

internal sealed class RecordingAdminApiService : IAdminApiService
{
    public bool Accepting { get; private set; }
    public bool Started { get; private set; }

    public Task StartAsync()
    {
        Started = true;

        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        Started = false;

        return Task.CompletedTask;
    }

    public void Activate()
        => Accepting = true;

    public void StopAccepting()
        => Accepting = false;
}
