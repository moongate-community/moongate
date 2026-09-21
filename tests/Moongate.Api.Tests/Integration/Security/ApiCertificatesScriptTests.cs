using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Moongate.Api.Data.Security;
using Moongate.Api.Security.Internal;
using Moongate.Api.Tests.TestSupport.Security;

namespace Moongate.Api.Tests.Integration.Security;

public class ApiCertificatesScriptTests
{
    [OpenSslFact]
    public async Task IssuedCertificates_PassTheApiTlsPolicy_InBothDirections()
    {
        using var certificates = new ScriptedCertificates();
        AssertSucceeds(await certificates.RunAsync("init"));
        AssertSucceeds(await certificates.RunAsync("issue", "server", "localhost"));
        AssertSucceeds(await certificates.RunAsync("issue", "client", "admin-console"));
        using var root = certificates.LoadRoot();
        using var server = certificates.LoadLeaf("localhost");
        using var client = certificates.LoadLeaf("admin-console");
        using var serverPolicy = new ApiTlsPolicy(TestCertificateAuthority.Options(root, server, client, "admin"));
        using var clientPolicy = new ApiTlsPolicy(TestCertificateAuthority.Options(root, client, server, "game"));

        var identities = await HandshakeAsync(serverPolicy, clientPolicy);

        Assert.Equal("admin", identities.Server.PeerId);
        Assert.Equal("game", identities.Client.PeerId);
    }

    [OpenSslFact]
    public async Task ClientCertificate_IsRejectedAsAServer()
    {
        using var certificates = new ScriptedCertificates();
        AssertSucceeds(await certificates.RunAsync("init"));
        AssertSucceeds(await certificates.RunAsync("issue", "client", "localhost"));
        AssertSucceeds(await certificates.RunAsync("issue", "client", "admin-console"));
        using var root = certificates.LoadRoot();
        using var server = certificates.LoadLeaf("localhost");
        using var client = certificates.LoadLeaf("admin-console");
        using var serverPolicy = new ApiTlsPolicy(TestCertificateAuthority.Options(root, server, client, "admin"));
        using var clientPolicy = new ApiTlsPolicy(TestCertificateAuthority.Options(root, client, server, "game"));

        await Assert.ThrowsAnyAsync<AuthenticationException>(() => HandshakeAsync(serverPolicy, clientPolicy));
    }

    [OpenSslFact]
    public async Task Fingerprint_MatchesWhatTheRuntimeComputes_AndThePfxLoads()
    {
        using var certificates = new ScriptedCertificates();
        AssertSucceeds(await certificates.RunAsync("init"));
        var issued = await certificates.RunAsync("issue", "server", "localhost");
        AssertSucceeds(issued);
        using var leaf = certificates.LoadLeaf("localhost");
        using var pfx = certificates.LoadPfx("localhost");
        var expected = leaf.GetCertHashString(HashAlgorithmName.SHA256);

        var printed = await certificates.RunAsync("fingerprint", "localhost");

        AssertSucceeds(printed);
        Assert.Equal(expected, printed.Output.Trim());
        Assert.Contains("PeersByCertificateSha256: " + expected, issued.Output, StringComparison.Ordinal);
        Assert.True(pfx.HasPrivateKey);
        Assert.Equal(leaf.Thumbprint, pfx.Thumbprint);
        Assert.Equal("localhost", leaf.GetNameInfo(X509NameType.DnsName, false));
    }

    [OpenSslFact]
    public async Task Init_RefusesToOverwriteAnExistingAuthority_AndIssueNeedsOne()
    {
        using var certificates = new ScriptedCertificates();
        var missing = await certificates.RunAsync("issue", "server", "localhost");
        AssertSucceeds(await certificates.RunAsync("init"));

        var again = await certificates.RunAsync("init");

        Assert.NotEqual(0, missing.ExitCode);
        Assert.Contains("run 'init' first", missing.Output, StringComparison.Ordinal);
        Assert.NotEqual(0, again.ExitCode);
        Assert.Contains("already exists", again.Output, StringComparison.Ordinal);
    }

    [OpenSslFact]
    public async Task Issue_RefusesToOverwriteAnExistingLeaf_AndKeepsIt()
    {
        using var certificates = new ScriptedCertificates();
        AssertSucceeds(await certificates.RunAsync("init"));
        AssertSucceeds(await certificates.RunAsync("issue", "client", "admin-console"));
        using var first = certificates.LoadLeaf("admin-console");

        var again = await certificates.RunAsync("issue", "client", "admin-console");

        Assert.NotEqual(0, again.ExitCode);
        Assert.Contains("admin-console.crt already exists", again.Output, StringComparison.Ordinal);
        using var stillThere = certificates.LoadLeaf("admin-console");
        Assert.Equal(first.Thumbprint, stillThere.Thumbprint);
    }

    [OpenSslFact]
    public async Task Issue_WithoutAUsableAuthority_LeavesNoPartialLeafBehind()
    {
        using var certificates = new ScriptedCertificates();
        AssertSucceeds(await certificates.RunAsync("init"));
        await File.WriteAllTextAsync(Path.Combine(certificates.OutputDirectory, "ca.key"), "not a key");

        var failed = await certificates.RunAsync("issue", "server", "localhost");

        Assert.NotEqual(0, failed.ExitCode);
        Assert.Contains("removed the partially issued files", failed.Output, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(certificates.OutputDirectory, "localhost.key")));
        Assert.False(File.Exists(Path.Combine(certificates.OutputDirectory, "localhost.crt")));
    }

    [OpenSslFact]
    public async Task PrivateKeys_AreOwnerReadableOnly()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var certificates = new ScriptedCertificates();
        AssertSucceeds(await certificates.RunAsync("init"));
        AssertSucceeds(await certificates.RunAsync("issue", "client", "admin-console"));

        foreach (var file in new[] { "ca.key", "admin-console.key", "admin-console.pfx" })
        {
            var mode = File.GetUnixFileMode(Path.Combine(certificates.OutputDirectory, file));
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, mode);
        }
    }

    private static void AssertSucceeds((int ExitCode, string Output) result)
    {
        Assert.True(result.ExitCode == 0, result.Output);
    }

    private static async Task<(ApiPeerIdentity Server, ApiPeerIdentity Client)> HandshakeAsync(
        ApiTlsPolicy serverPolicy, ApiTlsPolicy clientPolicy
    )
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var client = new TcpClient();
        await client.ConnectAsync((IPEndPoint)listener.LocalEndpoint, timeout.Token);
        using var server = await listener.AcceptTcpClientAsync(timeout.Token);
        ApiPeerIdentity? serverPeer = null;
        ApiPeerIdentity? clientPeer = null;
        var serverTask = serverPolicy.PrepareServerAsync(server.GetStream(), peer => serverPeer = peer, timeout.Token)
            .AsTask();
        var clientTask = clientPolicy.PrepareClientAsync(
                client.GetStream(),
                "localhost",
                "game",
                peer => clientPeer = peer,
                timeout.Token
            )
            .AsTask();

        try
        {
            await Task.WhenAll(serverTask, clientTask);

            return (Assert.IsType<ApiPeerIdentity>(serverPeer), Assert.IsType<ApiPeerIdentity>(clientPeer));
        }
        finally
        {
            if (serverTask.IsCompletedSuccessfully)
            {
                await serverTask.Result.DisposeAsync();
            }

            if (clientTask.IsCompletedSuccessfully)
            {
                await clientTask.Result.DisposeAsync();
            }
        }
    }
}
