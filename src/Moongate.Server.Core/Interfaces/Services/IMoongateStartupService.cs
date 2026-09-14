namespace Moongate.Server.Core.Interfaces.Services;

public interface IMoongateStartupService : IMoongateService
{
    Task StartAsync();

    Task StopAsync();
}
