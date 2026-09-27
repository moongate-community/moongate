using Moongate.Core.Utils;
using Moongate.Server.Data.Config;
using Moongate.Server.Data.Config.Sections;
using Moongate.Server.Types.Network;

namespace Moongate.Tests.Server.Data.Config.Sections;

public sealed class NetworkEncryptionConfigTests
{
    [Fact]
    public void Validate_DefaultConfigKeepsExistingUnencryptedConnections()
    {
        var config = new MoongateServerConfig();
        config.Validate();
        Assert.Equal(NetworkEncryptionMode.Disabled, config.Network.Encryption.Mode);
        Assert.Contains("Disabled", config.Network.Encryption.GetDescription());
    }

    [Fact]
    public void Validate_EnabledModeRequiresUsableVersionAndRejectsInvalidMode()
    {
        foreach (var version in new[] { "", "none", "ignition", "uorice", "garbage" })
        {
            var config = new NetworkEncryptionConfig { Mode = NetworkEncryptionMode.Required, ClientVersion = version };
            Assert.Throws<InvalidOperationException>(config.Validate);
        }
        Assert.Throws<InvalidOperationException>(() => new NetworkEncryptionConfig { Mode = (NetworkEncryptionMode)99 }.Validate());
        var server = new MoongateServerConfig();
        server.Network.Encryption = null!;
        Assert.Throws<InvalidOperationException>(server.Validate);
    }

    [Fact]
    public void Toml_EnhancedVersionRoundTripsAndAppearsInStartupDescription()
    {
        var config = TomlUtils.Deserialize<MoongateServerConfig>("""
            [network.encryption]
            mode = "Optional"
            client_version = "67.0.117.0"
            """)!;
        config.Validate();
        var roundtrip = TomlUtils.Deserialize<MoongateServerConfig>(TomlUtils.Serialize(config))!;
        Assert.Equal(NetworkEncryptionMode.Optional, roundtrip.Network.Encryption.Mode);
        Assert.Equal("67.0.117.0", roundtrip.Network.Encryption.ClientVersion);
        var message = roundtrip.Network.Encryption.GetDescription();
        Assert.Contains("Optional", message);
        Assert.Contains("67.0.117.0", message);
        Assert.Contains("Twofish", message);
    }
}
