using Moongate.Server.Data.Config;
using Moongate.Server.Data.Config.Sections;

namespace Moongate.Tests.Server.Data.Config.Sections;

public sealed class ApiConfigTests
{
    [Fact]
    public void Validate_DisabledWithIncompleteTls_DoesNotRequireCertificates()
    {
        new ApiConfig().Validate();
    }

    [Theory, InlineData(0), InlineData(-1), InlineData(65536)]
    public void Validate_EnabledWithInvalidPort_Rejects(int port)
    {
        var config = ValidConfig();
        config.Port = port;
        Assert.Throws<InvalidOperationException>(config.Validate);
    }

    [Theory, InlineData(""), InlineData("localhost"), InlineData("invalid")]
    public void Validate_EnabledWithInvalidAddress_Rejects(string address)
    {
        var config = ValidConfig();
        config.ListenAddress = address;
        Assert.Throws<InvalidOperationException>(config.Validate);
    }

    [Theory, InlineData("certificate"), InlineData("roots"), InlineData("blank_root"),
     InlineData("peers"), InlineData("fingerprint"), InlineData("identity"),
     InlineData("zero_operation"), InlineData("duplicate"), InlineData("password_variable")]
    public void Validate_EnabledWithInvalidTlsPolicy_Rejects(string invalidField)
    {
        var config = ValidConfig();
        switch (invalidField)
        {
            case "certificate": config.CertificatePath = " "; break;
            case "roots": config.TrustedRootPaths = []; break;
            case "blank_root": config.TrustedRootPaths = [" "]; break;
            case "peers": config.Peers = []; break;
            case "fingerprint": config.Peers[0].CertificateSha256 = new string('Z', 64); break;
            case "identity": config.Peers[0].PeerId = " "; break;
            case "zero_operation": config.Peers[0].AllowedOperations = [0]; break;
            case "duplicate": config.Peers = [config.Peers[0], new ApiPeerConfig
                { CertificateSha256 = new string('a', 64), PeerId = "second" }]; break;
            case "password_variable": config.CertificatePasswordEnvironmentVariable = null!; break;
        }
        Assert.Throws<InvalidOperationException>(config.Validate);
    }

    [Fact]
    public void Validate_NoAllowedOperationsAndUnencryptedPfx_AcceptsExplicitPolicy()
    {
        var config = ValidConfig();
        config.CertificatePasswordEnvironmentVariable = "";
        config.Peers[0].AllowedOperations = [];
        config.Validate();
    }

    [Fact]
    public void Validate_NullApiSection_Rejects()
    {
        var config = new MoongateServerConfig { Api = null! };
        Assert.Throws<InvalidOperationException>(config.Validate);
    }

    private static ApiConfig ValidConfig()
        => new()
        {
            Enabled = true,
            CertificatePath = "server.pfx",
            TrustedRootPaths = ["root.pem"],
            Peers = [new ApiPeerConfig
            {
                CertificateSha256 = new string('A', 64), PeerId = "peer", AllowedOperations = [100]
            }]
        };
}
