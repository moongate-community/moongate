using DryIoc;

namespace Moongate.Core.Server.Bootstrap;

public class MoongateBootstrap
{
    private readonly IContainer _container;

    public event MoongateBootstrapDelegates.ConfigureServicesDelegate? ConfigureServices;
    public event MoongateBootstrapDelegates.ConfigureNetworkServicesDelegate ConfigureNetworkServices;

    public MoongateBootstrap()
    {
        _container = new Container(rules =>
            rules.WithUseInterpretation()
        );
    }

    public void Initialize()
    {
        ConfigureServices?.Invoke(_container);
        // Perform initialization tasks such as loading configurations,
        // setting up services, etc.
    }

    public Task StartAsync()
    {
        // Start the server or application logic.

        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        // Clean up resources and stop the server or application logic.

        return Task.CompletedTask;

    }

}
