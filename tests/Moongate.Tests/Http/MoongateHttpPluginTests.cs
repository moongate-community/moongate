using DryIoc;
using Moongate.Http.Plugin;
using Moongate.Http.Plugin.Data.Config;
using Moongate.Http.Plugin.Interfaces;
using Moongate.Http.Plugin.Services;

namespace Moongate.Tests.Http;

public class MoongateHttpPluginTests
{
    /// <summary>A container carrying what the real bootstrap supplies around the plugin.</summary>
    private static IContainer Configured()
    {
        var container = new Container();
        container.RegisterInstance(new MoongateHttpConfig());
        container.RegisterInstance(TimeProvider.System);

        new MoongateHttpPlugin().Configure(container, new());

        return container;
    }

    [Fact]
    public void Configure_RegistersTheTokenService()
        => Assert.IsType<JwtTokenService>(Configured().Resolve<IJwtTokenService>());

    [Fact]
    public void Configure_RegistersTheServerThatRunsTheApi()
        => Assert.NotNull(Configured().Resolve<HttpServerService>());

    [Fact]
    public void Metadata_IdentifiesThePlugin()
        => Assert.Equal("moongate.http.plugin", new MoongateHttpPlugin().Metadata.Id);
}
