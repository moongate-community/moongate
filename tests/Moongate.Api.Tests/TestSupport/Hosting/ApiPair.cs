using System.Net;
using Moongate.Api.Client;
using Moongate.Api.Data.Config;
using Moongate.Api.Interfaces.Connections;
using Moongate.Api.Registry;
using Moongate.Api.Server;
using Moongate.Api.Tests.TestSupport.Contracts;
using Moongate.Api.Tests.TestSupport.Handlers;
using Moongate.Api.Tests.TestSupport.Security;
namespace Moongate.Api.Tests.TestSupport.Hosting;
internal sealed class ApiPair : IAsyncDisposable
{
    public ApiServer Server { get; }
    public ApiClient Client { get; }
    public IApiConnection ClientConnection { get; private set; } = null!;
    public IApiConnection ServerConnection { get; private set; } = null!;
    private ApiPair(ApiServer server, ApiClient client) { Server = server; Client = client; }
    public static async Task<ApiPair> StartAsync(GatedHandler? handler = null, ApiOptions? options = null, TimeProvider? clock = null)
    {
        using var ca = new TestCertificateAuthority();
        using var serverCert = ca.Issue();
        using var clientCert = ca.Issue();
        var serverRegistry = new ApiRegistry();
        if (handler is null) { serverRegistry.RegisterHandler(() => new IncrementHandler()); }
        else { serverRegistry.RegisterHandler(() => handler); }
        var clientRegistry = new ApiRegistry();
        clientRegistry.RegisterHandler(() => new IncrementHandler());
        var server = new ApiServer(new IPEndPoint(IPAddress.Loopback, 0), serverRegistry, options ?? new ApiOptions(), ca.Options(serverCert, clientCert, "admin"), clock ?? TimeProvider.System);
        var client = new ApiClient(clientRegistry, options ?? new ApiOptions(), ca.Options(clientCert, serverCert, "game"), clock ?? TimeProvider.System);
        var pair = new ApiPair(server, client);
        try
        {
            await server.StartAsync();
            pair.ClientConnection = await client.ConnectAsync(server.Endpoint!, "localhost", "game");
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (server.Connections.Count != 1) { await Task.Delay(1, timeout.Token); }
            pair.ServerConnection = server.Connections[0];
            return pair;
        }
        catch { await pair.DisposeAsync(); throw; }
    }
    public async ValueTask DisposeAsync()
    {
        try { await Client.DisposeAsync(); }
        finally { await Server.DisposeAsync(); }
    }
}
