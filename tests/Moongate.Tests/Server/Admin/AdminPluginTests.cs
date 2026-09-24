using DryIoc;
using Moongate.Core.Directories;
using Moongate.Server.Admin;
using Moongate.Server.Admin.Data.Config;
using Moongate.Server.Core.Interfaces.Admin;
using Moongate.Server.Core.Types.Hosting;
using Moongate.Tests.TestSupport.Persistence;

namespace Moongate.Tests.Server.Admin;

public sealed class AdminPluginTests
{
    [Fact]
    public async Task Register_DisabledGamePlugin_DoesNotResolveAccountOrCertificateDependencies()
    {
        using var directory = new TemporaryPersistenceDirectory();
        using var container = new Container();
        container.RegisterInstance(new DirectoriesConfig(directory.Path, []));
        container.RegisterInstance(ServerMode.Game);
        container.RegisterInstance(new AdminApiConfig { CertificatePassword = "$ADMIN_UNDEFINED_TEST_ENV" });
        var plugin = new MoongateAdminPlugin();
        Assert.Equal(
            "com.github.moongate-community.moongate.plugins.ultima",
            Assert.Single(plugin.Metadata.Dependencies).Id
        );
        plugin.Register(container);
        var host = container.Resolve<IAdminApiService>();
        await host.StartAsync();
        host.Activate();
        host.StopAccepting();
        await host.StopAsync();
    }
}
