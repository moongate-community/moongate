using Moongate.Network.Interfaces.Client;
using Moongate.Server.Services.Network;

namespace Moongate.Tests.TestSupport.Network;

internal sealed class ConnectionRegistryFixture : IAsyncDisposable
{
    public ConnectionService Service { get; } = new();

    private ConnectionRegistryFixture()
    {
    }

    public static async Task<ConnectionRegistryFixture> CreateAsync(params INetworkConnection[] connections)
    {
        var fixture = new ConnectionRegistryFixture();
        await fixture.Service.StartAsync();
        foreach (var connection in connections)
        {
            if (!fixture.Service.TryRegister(connection))
            {
                await fixture.DisposeAsync();
                throw new InvalidOperationException("Could not register the test connection.");
            }
        }

        return fixture;
    }

    public async ValueTask DisposeAsync()
    {
        await Service.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
    }
}
