namespace Moongate.Server.Core.Interfaces;

public interface IMoongateServerBootstrap
{
    Task StartAsync();

    Task StopAsync();

    Task RunAsync();

}
