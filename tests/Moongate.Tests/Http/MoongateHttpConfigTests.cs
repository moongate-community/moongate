using Moongate.Http.Plugin.Data.Config;

namespace Moongate.Tests.Http;

public class MoongateHttpConfigTests
{
    [Fact]
    public void Defaults_BindEverywhereOn8933()
    {
        var config = new MoongateHttpConfig();

        Assert.Equal("0.0.0.0", config.Address);
        Assert.Equal(8933, config.Port);
    }

    [Fact]
    public void Defaults_LeaveJwtPresentSoConfigNeedsNoHttpSection()
    {
        // moongate.yaml needs no `http:` section for the plugin to run; only SigningKey must be
        // supplied, and it deliberately has no usable default.
        var config = new MoongateHttpConfig();

        Assert.NotNull(config.Jwt);
        Assert.Equal(60, config.Jwt.LifetimeMinutes);
        Assert.Equal("moongate", config.Jwt.Issuer);
        Assert.Empty(config.Jwt.SigningKey);
    }
}
