using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Moongate.Api.Client;
using Moongate.Api.Registry;
using Moongate.Core.Directories;
using Moongate.Server.Core.Data.Realms.Api;
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
    private readonly List<X509Certificate2> _additionalClients = [];
    private readonly string _passwordVariable = $"MOONGATE_TEST_API_{Guid.NewGuid():N}";

    public ApiConfig Config { get; }
    public DirectoriesConfig Directories { get; }
    public ApiRegistry Registry { get; } = new();
    public string Password { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

    public string ServerFingerprint => _server.GetCertHashString(HashAlgorithmName.SHA256);

    public ApiHostFixture()
    {
        _server = _authority.Issue();
        _client = _authority.Issue();
        Directories = new(_directory.Path, ["config"]);
        var certificateDirectory = Path.Combine(Directories["config"], "tls");
        Directory.CreateDirectory(certificateDirectory);
        File.WriteAllBytes(Path.Combine(certificateDirectory, "server.pfx"), _server.Export(X509ContentType.Pfx, Password));
        File.WriteAllText(Path.Combine(certificateDirectory, "root.pem"), _authority.Root.ExportCertificatePem());
        System.Environment.SetEnvironmentVariable(_passwordVariable, Password);
        var reservation = new TcpListener(IPAddress.Loopback, 0);
        reservation.Start();
        var port = ((IPEndPoint)reservation.LocalEndpoint).Port;
        reservation.Stop();
        Config = new()
        {
            Enabled = true,
            ListenAddress = "127.0.0.1",
            Port = port,
            CertificatePath = "tls/server.pfx",
            CertificatePasswordEnvironmentVariable = _passwordVariable,
            TrustedRootPaths = ["tls/root.pem"],
            Peers =
            [
                new()
                {
                    CertificateSha256 = _client.GetCertHashString(HashAlgorithmName.SHA256),
                    PeerId = "client", AllowedOperations = new([100])
                }
            ]
        };
        Registry.RegisterHandler(() => new IncrementHandler());
    }

    public void AssertPortReleased()
    {
        var listener = new TcpListener(IPAddress.Loopback, Config.Port);

        try
        {
            listener.Start();
        }
        finally
        {
            listener.Stop();
        }
    }

    public ApiClient CreateClient(Action<ApiRegistry>? configureRegistry = null)
    {
        var registry = new ApiRegistry();
        registry.RegisterContract<IncrementRequest, IncrementResponse>();
        configureRegistry?.Invoke(registry);

        return new(
            registry,
            new(),
            _authority.Options(_client, _server, "server"),
            TimeProvider.System
        );
    }

    public ApiClient CreateRealmClient(string realmId)
    {
        var certificate = _authority.Issue();
        _additionalClients.Add(certificate);
        Config.Peers =
        [
            .. Config.Peers,
            new()
            {
                CertificateSha256 = certificate.GetCertHashString(HashAlgorithmName.SHA256),
                PeerId = realmId,
                AllowedOperations = new([0x0100, 0x0101, 0x0102])
            }
        ];
        var registry = new ApiRegistry();
        registry.RegisterContract<RegisterRealmRequest, RegisterRealmResponse>();
        registry.RegisterContract<RenewRealmRequest, RenewRealmResponse>();
        registry.RegisterContract<UnregisterRealmRequest, UnregisterRealmResponse>();
        return new(registry, new(), _authority.Options(certificate, _server, "server"), TimeProvider.System);
    }

    public ApiServerService CreateService()
        => new(Config, Directories, Registry, TimeProvider.System);

    public void Dispose()
    {
        System.Environment.SetEnvironmentVariable(_passwordVariable, null);
        _client.Dispose();
        foreach (var certificate in _additionalClients)
        {
            certificate.Dispose();
        }
        _server.Dispose();
        _authority.Dispose();
        _directory.Dispose();
    }

    public void UseInvalidServerCertificate(string invalidity)
    {
        using var certificate = _authority.Issue(
            expired: invalidity == "expired",
            clientOnly: invalidity == "client_only",
            notYetValid: invalidity == "future"
        );
        using var publicCertificate = X509CertificateLoader.LoadCertificate(certificate.Export(X509ContentType.Cert));
        var exported = invalidity == "no_private_key" ? publicCertificate : certificate;
        File.WriteAllBytes(
            Path.Combine(Directories["config"], Config.CertificatePath),
            exported.Export(X509ContentType.Pfx, Password)
        );
    }

    public void UseUnencryptedCertificate()
    {
        File.WriteAllBytes(Path.Combine(Directories["config"], Config.CertificatePath), _server.Export(X509ContentType.Pfx));
        Config.CertificatePasswordEnvironmentVariable = "";
    }
}
