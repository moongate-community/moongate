using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using Moongate.Server.Bootstrap.Internal.Setup;
using Moongate.Tests.TestSupport.Directories;

namespace Moongate.Tests.Integration.Boot;

public sealed class AdminCertificateSetupTests
{
    [Fact]
    public async Task Configure_GeneratedIdentity_CompletesTlsWithPublicCertificateTrust()
    {
        using var directory = new TemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(directory.Path, "config"));
        File.WriteAllText(Path.Combine(directory.Path, "config/moongate.toml"), "[admin_api]\n");
        AdminCertificateSetup.Configure(directory.Path, ["admin.example.test"], TextWriter.Null);
        using var identity = X509CertificateLoader.LoadPkcs12FromFile(
            Path.Combine(directory.Path, "certificates/admin.pfx"),
            ""
        );
        using var trust =
            X509CertificateLoader.LoadCertificateFromFile(Path.Combine(directory.Path, "certificates/admin.crt"));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var client = new TcpClient();
        var accept = listener.AcceptTcpClientAsync(timeout.Token);
        await client.ConnectAsync((IPEndPoint)listener.LocalEndpoint, timeout.Token);
        using var server = await accept;
        using var serverStream = new SslStream(server.GetStream());
        using var clientStream = new SslStream(client.GetStream());
        var serverHandshake = serverStream.AuthenticateAsServerAsync(
            new()
            {
                ServerCertificate = identity,
                ClientCertificateRequired = false,
                ApplicationProtocols = [SslApplicationProtocol.Http2]
            },
            timeout.Token
        );
        var clientHandshake = clientStream.AuthenticateAsClientAsync(
            new()
            {
                TargetHost = "admin.example.test",
                ApplicationProtocols = [SslApplicationProtocol.Http2],
                CertificateChainPolicy = new()
                {
                    TrustMode = X509ChainTrustMode.CustomRootTrust,
                    CustomTrustStore = { trust },
                    RevocationMode = X509RevocationMode.NoCheck
                }
            },
            timeout.Token
        );
        await Task.WhenAll(serverHandshake, clientHandshake);
        Assert.True(clientStream.IsAuthenticated);
        Assert.Equal(SslApplicationProtocol.Http2, clientStream.NegotiatedApplicationProtocol);
    }
}
