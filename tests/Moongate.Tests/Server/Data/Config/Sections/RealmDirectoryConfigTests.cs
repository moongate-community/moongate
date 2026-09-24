using Moongate.Server.Core.Types.Hosting;
using Moongate.Server.Data.Config.Sections;

namespace Moongate.Tests.Server.Data.Config.Sections;

public sealed class RealmDirectoryConfigTests
{
    [Fact]
    public void Validate_StandaloneDefaults_AreUsable()
    {
        new RealmDirectoryConfig().Validate(ServerMode.Standalone);
    }

    [Theory, InlineData(""), InlineData("0.0.0.0"), InlineData("224.0.0.1"), InlineData("::1")]
    public void Validate_GameRequiresClientFacingIpv4(string address)
    {
        var config = CreateGameConfig();
        config.AdvertisedAddress = address;

        Assert.Contains("advertised_address", Assert.Throws<InvalidOperationException>(() =>
            config.Validate(ServerMode.Game)).Message);
    }

    [Fact]
    public void Validate_GameRequiresClientFacingPort()
    {
        var config = CreateGameConfig();
        config.AdvertisedPort = 0;

        Assert.Contains("advertised_port", Assert.Throws<InvalidOperationException>(() =>
            config.Validate(ServerMode.Game)).Message);
    }

    [Fact]
    public void Validate_LeaseMustSurviveAtLeastOneMissedHeartbeat()
    {
        var config = new RealmDirectoryConfig { LeaseDurationSeconds = 9 };

        Assert.Contains("lease_duration_seconds", Assert.Throws<InvalidOperationException>(() =>
            config.Validate(ServerMode.Standalone)).Message);
    }

    [Fact]
    public void Validate_LoginIgnoresGameEndpoint()
    {
        new RealmDirectoryConfig().Validate(ServerMode.Login);
    }

    private static RealmDirectoryConfig CreateGameConfig()
        => new()
        {
            RealmId = "realm-a", Name = "Realm A", ServerIndex = 1,
            AdvertisedAddress = "127.0.0.1", AdvertisedPort = 2593
        };
}
