namespace Moongate.Core.Server.Interfaces.Services.Base;

public interface IMoongateAutostartService : IMoongateService
{
    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}
