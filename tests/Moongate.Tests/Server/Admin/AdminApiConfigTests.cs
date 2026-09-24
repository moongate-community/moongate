using Moongate.Core.Utils;
using Moongate.Server.Admin.Data.Config;
using Moongate.Server.Data.Config;

namespace Moongate.Tests.Server.Admin;

public class AdminApiConfigTests
{
    [Fact]
    public void ServerConfig_AdminSection_BindsSnakeCaseAndValidates()
    {
        var config = TomlUtils.Deserialize<MoongateServerConfig>(
            "[admin_api]\nenabled=true\nport=2591\nallow_insecure_loopback=true"
        )!;
        config.Validate();
        Assert.True(config.AdminApi.Enabled);
        Assert.Equal(2591, config.AdminApi.Port);
        var copy = TomlUtils.Deserialize<MoongateServerConfig>(TomlUtils.Serialize(config))!;
        Assert.Equal(2591, copy.AdminApi.Port);
        copy.AdminApi.Port = 0;
        Assert.Throws<InvalidOperationException>(copy.Validate);
    }

    [Theory, InlineData("*"), InlineData("0.0.0.0"), InlineData("::")]
    public void Validate_TlsWildcardToml_PreservesAddress(string address)
    {
        var config = TomlUtils.Deserialize<AdminApiConfig>($"enabled = true\nlisten_address = '{address}'\n")!;
        config.Validate();
        var copy = TomlUtils.Deserialize<AdminApiConfig>(TomlUtils.Serialize(config))!;
        Assert.Equal(address, copy.ListenAddress);
        Assert.False(copy.AllowInsecureLoopback);
    }

    [Fact]
    public void Defaults_DisableListenerAndUseApprovedPort()
    {
        var config = new AdminApiConfig();
        Assert.False(config.Enabled);
        Assert.Equal(2590, config.Port);
        Assert.Equal("127.0.0.1", config.ListenAddress);
        Assert.Equal(30, config.SessionLifetimeMinutes);
        config.Validate();
    }

    [Theory, InlineData("*"), InlineData("0.0.0.0"), InlineData("::"), InlineData("192.168.1.10"), InlineData("localhost")]
    public void Validate_InsecureNonLoopback_Rejects(string address)
    {
        var config = new AdminApiConfig { ListenAddress = address, AllowInsecureLoopback = true };
        Assert.Throws<InvalidOperationException>(config.Validate);
    }

    [Theory, InlineData("127.0.0.1"), InlineData("::1")]
    public void Validate_InsecureLiteralLoopback_Accepts(string address)
        => new AdminApiConfig { ListenAddress = address, AllowInsecureLoopback = true }.Validate();

    [Theory, InlineData(0, 30, 65536, 64), InlineData(65536, 30, 65536, 64),
     InlineData(2590, 0, 65536, 64), InlineData(2590, 1441, 65536, 64),
     InlineData(2590, 30, 1023, 64), InlineData(2590, 30, 1048577, 64),
     InlineData(2590, 30, 65536, 0), InlineData(2590, 30, 65536, 1025)]
    public void Validate_OutOfBounds_Rejects(int port, int lifetime, int bytes, int concurrency)
    {
        var config = new AdminApiConfig
        {
            Port = port, SessionLifetimeMinutes = lifetime,
            MaxReceiveMessageBytes = bytes, MaxConcurrentCalls = concurrency
        };
        Assert.Throws<InvalidOperationException>(config.Validate);
    }

    [Fact]
    public void Validate_UnresolvedCertificateSecret_DoesNotResolve()
        => new AdminApiConfig { CertificatePassword = "${MISSING_ADMIN_TEST_PASSWORD}" }.Validate();

    [Fact]
    public void Toml_RoundTrip_PreservesConfiguration()
    {
        const string toml =
            "enabled = true\nlisten_address = '::1'\nport = 2591\nallow_insecure_loopback = true\nsession_lifetime_minutes = 45\n";
        var config = TomlUtils.Deserialize<AdminApiConfig>(toml)!;
        var copy = TomlUtils.Deserialize<AdminApiConfig>(TomlUtils.Serialize(config))!;
        copy.Validate();
        Assert.True(copy.Enabled);
        Assert.Equal("::1", copy.ListenAddress);
        Assert.Equal(2591, copy.Port);
        Assert.True(copy.AllowInsecureLoopback);
        Assert.Equal(45, copy.SessionLifetimeMinutes);
    }
}
