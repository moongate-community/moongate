using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Moongate.Api.Client;
using Moongate.Api.Data.Config;
using Moongate.Api.Registry;
using Moongate.Core.Directories;
using Moongate.Server.Data.Config.Sections;
using Moongate.Server.Services.Api;
using Moongate.Tests.TestSupport.Directories;

namespace Moongate.Tests.TestSupport.Api;

internal sealed class ApiHostFixture : IDisposable
{
    private readonly TemporaryDirectory _directory = new();
    private readonly ApiTestCertificateAuthority _authority = new();
    private readonly X509Certificate2 _server;
    private readonly X509Certificate2 _client;
    private readonly string _passwordVariable = $"MOONGATE_TEST_API_{Guid.NewGuid():N}";

    public ApiConfig Config { get; }
    public DirectoriesConfig Directories { get; }
    public ApiRegistry Registry { get; } = new();
    public string Password { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

    public ApiHostFixture()
    {
        _server = _authority.Issue();
        _client = _authority.Issue();
        Directories = new DirectoriesConfig(_directory.Path, ["config"]);
        var certificateDirectory = Path.Combine(Directories["config"], "tls");
        Directory.CreateDirectory(certificateDirectory);
        File.WriteAllBytes(Path.Combine(certificateDirectory, "server.pfx"), _server.Export(X509ContentType.Pfx, Password));
        File.WriteAllText(Path.Combine(certificateDirectory, "root.pem"), _authority.Root.ExportCertificatePem());
        System.Environment.SetEnvironmentVariable(_passwordVariable, Password);
        var reservation = new TcpListener(IPAddress.Loopback, 0);
        reservation.Start();
        var port = ((IPEndPoint)reservation.LocalEndpoint).Port;
        reservation.Stop();
        Config = new ApiConfig
        {
            Enabled = true,
            ListenAddress = "127.0.0.1",
            Port = port,
            CertificatePath = "tls/server.pfx",
            CertificatePasswordEnvironmentVariable = _passwordVariable,
            TrustedRootPaths = ["tls/root.pem"],
            Peers = [new ApiPeerConfig
            {
                CertificateSha256 = _client.GetCertHashString(HashAlgorithmName.SHA256),
                PeerId = "client", AllowedOperations = [100]
            }]
        };
        Registry.RegisterHandler(() => new IncrementHandler());
    }

    public ApiServerService CreateService()
        => new(Config, Directories, Registry, TimeProvider.System);

    public ApiClient CreateClient()
    {
        var registry = new ApiRegistry();
        registry.RegisterContract<IncrementRequest, IncrementResponse>();
        return new ApiClient(registry, new ApiOptions(), _authority.Options(_client, _server, "server"), TimeProvider.System);
    }

    public void UseInvalidServerCertificate(string invalidity)
    {
        using var certificate = _authority.Issue(expired: invalidity == "expired",
            clientOnly: invalidity == "client_only", notYetValid: invalidity == "future");
        using var publicCertificate = X509CertificateLoader.LoadCertificate(certificate.Export(X509ContentType.Cert));
        var exported = invalidity == "no_private_key" ? publicCertificate : certificate;
        File.WriteAllBytes(Path.Combine(Directories["config"], Config.CertificatePath), exported.Export(X509ContentType.Pfx, Password));
    }

    public void UseUnencryptedCertificate()
    {
        File.WriteAllBytes(Path.Combine(Directories["config"], Config.CertificatePath), _server.Export(X509ContentType.Pfx));
        Config.CertificatePasswordEnvironmentVariable = "";
    }

    public void AssertPortReleased()
    {
        var listener = new TcpListener(IPAddress.Loopback, Config.Port);
        try { listener.Start(); }
        finally { listener.Stop(); }
    }

    public void Dispose()
    {
        System.Environment.SetEnvironmentVariable(_passwordVariable, null);
        _client.Dispose();
        _server.Dispose();
        _authority.Dispose();
        _directory.Dispose();
    }
}
