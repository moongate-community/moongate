using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Moongate.Api.Data.Security;
using Moongate.Api.Security.Internal;
using Moongate.Api.Tests.TestSupport.Security;

namespace Moongate.Api.Tests.Integration.Security;

public class MutualTlsTests
{
    [Fact]
    public async Task Prepare_MutuallyAuthenticatesAndSnapshotsCallerOwnedOptions()
    {
        using var ca = new TestCertificateAuthority();
        using var serverCertificate = ca.Issue();
        using var clientCertificate = ca.Issue();
        var serverOptions = ca.Options(serverCertificate, clientCertificate, "admin");
        using var serverPolicy = new ApiTlsPolicy(serverOptions);
        using var clientPolicy = new ApiTlsPolicy(ca.Options(clientCertificate, serverCertificate, "game"));
        ((Dictionary<string, ApiPeerIdentity>)serverOptions.PeersByCertificateSha256).Clear();
        serverCertificate.Dispose();
        clientCertificate.Dispose();
        ca.Dispose();
        var identities = await HandshakeAsync(serverPolicy, clientPolicy);
        Assert.Equal("admin", identities.Server.PeerId);
        Assert.Equal("game", identities.Client.PeerId);
    }

    [Theory, InlineData("name"), InlineData("peer"), InlineData("expired"), InlineData("untrusted"), InlineData("unlisted"), InlineData("eku")]
    public async Task PrepareClient_RejectsInvalidServer(string failure)
    {
        using var ca = new TestCertificateAuthority();
        using var otherCa = new TestCertificateAuthority();
        using var serverCertificate = ca.Issue(failure == "name" ? "wrong.example" : "localhost", failure == "expired", failure == "eku");
        using var clientCertificate = ca.Issue();
        using var otherCertificate = ca.Issue();
        using var serverPolicy = new ApiTlsPolicy(ca.Options(serverCertificate, clientCertificate, "admin"));
        var clientOptions = ca.Options(clientCertificate, failure == "unlisted" ? otherCertificate : serverCertificate, "game");
        if (failure == "untrusted") { clientOptions = clientOptions with { TrustedRoots = new[] { otherCa.Root } }; }
        using var clientPolicy = new ApiTlsPolicy(clientOptions);
        await Assert.ThrowsAnyAsync<AuthenticationException>(() => HandshakeAsync(serverPolicy, clientPolicy, failure == "peer" ? "other" : "game"));
    }

    [Theory, InlineData("expired"), InlineData("untrusted"), InlineData("unlisted")]
    public async Task PrepareServer_RejectsInvalidClient(string failure)
    {
        using var ca = new TestCertificateAuthority();
        using var otherCa = new TestCertificateAuthority();
        using var serverCertificate = ca.Issue();
        using var clientCertificate = (failure == "untrusted" ? otherCa : ca).Issue(expired: failure == "expired");
        using var otherCertificate = ca.Issue();
        using var serverPolicy = new ApiTlsPolicy(ca.Options(serverCertificate, failure == "unlisted" ? otherCertificate : clientCertificate, "admin"));
        using var clientPolicy = new ApiTlsPolicy(ca.Options(clientCertificate, serverCertificate, "game"));
        await Assert.ThrowsAnyAsync<AuthenticationException>(() => HandshakeAsync(serverPolicy, clientPolicy));
    }

    [Fact]
    public async Task PrepareServer_RejectsAbsentCertificate()
    {
        using var ca = new TestCertificateAuthority();
        using var certificate = ca.Issue();
        using var policy = new ApiTlsPolicy(ca.Options(certificate, certificate, "peer"));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var client = new TcpClient();
        await client.ConnectAsync((IPEndPoint)listener.LocalEndpoint, timeout.Token);
        using var server = await listener.AcceptTcpClientAsync(timeout.Token);
        using var ssl = new SslStream(client.GetStream(), false);
        var serverTask = policy.PrepareServerAsync(server.GetStream(), _ => Assert.Fail("No identity may be published."), timeout.Token).AsTask();
        var clientTask = ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
        {
            TargetHost = "localhost",
            CertificateChainPolicy = new X509ChainPolicy
            {
                TrustMode = X509ChainTrustMode.CustomRootTrust,
                CustomTrustStore = { ca.Root },
                RevocationMode = X509RevocationMode.NoCheck,
                DisableCertificateDownloads = true
            }
        }, timeout.Token);
        await Assert.ThrowsAnyAsync<AuthenticationException>(() => serverTask);
        try { await clientTask; } catch (System.Security.Authentication.AuthenticationException) { }
    }

    private static async Task<(ApiPeerIdentity Server, ApiPeerIdentity Client)> HandshakeAsync(ApiTlsPolicy serverPolicy, ApiTlsPolicy clientPolicy, string expectedPeer = "game")
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var client = new TcpClient();
        await client.ConnectAsync((IPEndPoint)listener.LocalEndpoint, timeout.Token);
        using var server = await listener.AcceptTcpClientAsync(timeout.Token);
        ApiPeerIdentity? serverPeer = null;
        ApiPeerIdentity? clientPeer = null;
        var serverTask = serverPolicy.PrepareServerAsync(server.GetStream(), peer => serverPeer = peer, timeout.Token).AsTask();
        var clientTask = clientPolicy.PrepareClientAsync(client.GetStream(), "localhost", expectedPeer, peer => clientPeer = peer, timeout.Token).AsTask();
        try
        {
            await Task.WhenAll(serverTask, clientTask);
            return (Assert.IsType<ApiPeerIdentity>(serverPeer), Assert.IsType<ApiPeerIdentity>(clientPeer));
        }
        finally
        {
            if (serverTask.IsCompletedSuccessfully) { await serverTask.Result.DisposeAsync(); }
            if (clientTask.IsCompletedSuccessfully) { await clientTask.Result.DisposeAsync(); }
        }
    }
}
