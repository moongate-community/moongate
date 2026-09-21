using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Moongate.Api.Client;
using Moongate.Api.Data.Config;
using Moongate.Api.Data.Security;
using Moongate.Api.Exceptions;
using Moongate.Api.Registry;
using Moongate.Api.Server;
using Moongate.Api.TestHost.Data;
using Moongate.Api.TestHost.Handlers;

try
{
    var config = JsonSerializer.Deserialize<HostConfiguration>(
        await Console.In.ReadLineAsync() ?? throw new InvalidDataException()
    ) ?? throw new InvalidDataException();
    using var certificate = X509CertificateLoader.LoadPkcs12(
        Convert.FromBase64String(config.Certificate),
        null,
        X509KeyStorageFlags.EphemeralKeySet
    );
    using var root = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(config.Root));
    var tls = new ApiTlsOptions
    {
        Certificate = certificate,
        TrustedRoots = [root],
        PeersByCertificateSha256 = config.Peers.ToDictionary(
            pair => pair.Key,
            pair => new ApiPeerIdentity(pair.Value, config.Permissions[pair.Value])
        )
    };
    var registry = new ApiRegistry();
    if (args.Single() is "login" or "game")
    {
        var handler = new IncrementHandler();
        registry.RegisterHandler(() => handler);
        await using var server = new ApiServer(
            new IPEndPoint(IPAddress.Loopback, 0),
            registry,
            new ApiOptions(),
            tls,
            TimeProvider.System
        );
        await server.StartAsync();
        Console.WriteLine($"READY {server.Endpoint!.Port}");
        while (await Console.In.ReadLineAsync() is { } command && command != "STOP")
        {
            if (command == "COUNT")
            {
                Console.WriteLine($"COUNT {handler.Invocations}");
            }
        }

        await server.StopAsync();
    }
    else if (args[0] == "client")
    {
        registry.RegisterContract<IncrementRequest, IncrementResponse>();
        await using var client = new ApiClient(registry, new ApiOptions(), tls, TimeProvider.System);
        var connection = await client.ConnectAsync(new IPEndPoint(IPAddress.Loopback, config.Port), "localhost", "target");
        try
        {
            var result = await connection.RequestAsync<IncrementRequest, IncrementResponse>(
                new IncrementRequest { Value = config.Value }
            );
            Console.WriteLine(result.Value);
        }
        catch (ApiRemoteException error)
        {
            Console.WriteLine($"ERROR {error.Code}");
        }
        catch (IOException) when (config.ReconnectAfterLoss)
        {
            await connection.Completion;
            await using var replacement = await client.ConnectAsync(
                new IPEndPoint(IPAddress.Loopback, config.Port),
                "localhost",
                "target"
            );
            Console.WriteLine("DISCONNECTED RECONNECTED");
        }
    }
    else
    {
        throw new ArgumentException("Unknown test host role.");
    }

    return 0;
}
catch (Exception exception)
{
    // Synthetic certificate material must never be copied to process diagnostics.
    Console.Error.WriteLine(exception.GetType().Name);
    return 1;
}
