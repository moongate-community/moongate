namespace Moongate.Server.Core.Interfaces.Bootstrap;

public interface IMoongateServerBootstrap
{
    Task StartAsync();

    Task StopAsync();

    Task RunAsync();

}
