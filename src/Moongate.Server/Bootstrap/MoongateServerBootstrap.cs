using DryIoc;
using Moongate.Server.Core.Interfaces;
using Serilog;

namespace Moongate.Server.Bootstrap;

public class MoongateServerBootstrap : IMoongateServerBootstrap
{
    private readonly Container _container;

    private readonly ILogger _logger = Log.ForContext<MoongateServerBootstrap>();

    private readonly CancellationToken _cancellationToken;

    public MoongateServerBootstrap(Container container, CancellationToken cancellationToken)
    {
        _container = container;
        _cancellationToken = cancellationToken;
    }

    public Task StartAsync()
    {
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        _logger.Information("Moongate Server is stopping...");

        await Log.CloseAndFlushAsync();
    }

    public async Task RunAsync()
    {
        while (!_cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(100, _cancellationToken);
        }
    }
}
