using DryIoc;
using Moongate.Core.Directories;
using Moongate.Server.Admin;
using Moongate.Server.Admin.Data.Config;
using Moongate.Server.Core.Interfaces.Admin;
using Moongate.Server.Core.Types.Hosting;
using Moongate.Tests.TestSupport.Config;
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
        container.RegisterInstance(
            TestConfigDocuments.FromToml(directory.Path, "[admin_api]\ncertificate_password = \"$ADMIN_UNDEFINED_TEST_ENV\"\n")
        );
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

    [Fact]
    public void Register_ReadsTheAdminApiSection()
    {
        using var directory = new TemporaryPersistenceDirectory();
        using var container = Container(directory, "[admin_api]\nport = 2599\nsession_lifetime_minutes = 45\n");

        new MoongateAdminPlugin().Register(container);

        Assert.Equal(2599, container.Resolve<AdminApiConfig>().Port);
        Assert.Equal(45, container.Resolve<AdminApiConfig>().SessionLifetimeMinutes);
    }

    [Fact]
    public void Register_NoAdminApiSection_AppendsTheDefaults()
    {
        using var directory = new TemporaryPersistenceDirectory();
        using var container = Container(directory, "");

        new MoongateAdminPlugin().Register(container);

        Assert.False(container.Resolve<AdminApiConfig>().Enabled);
        Assert.Contains("[admin_api]", File.ReadAllText(Path.Combine(directory.Path, "moongate.toml")));
    }

    [Fact]
    public void Register_AnInvalidAdminApiSection_Throws()
    {
        using var directory = new TemporaryPersistenceDirectory();
        using var container = Container(directory, "[admin_api]\nlisten_address = \"not-an-ip\"\n");

        Assert.Throws<InvalidOperationException>(() => new MoongateAdminPlugin().Register(container));
    }

    private static Container Container(TemporaryPersistenceDirectory directory, string toml)
    {
        var container = new Container();
        container.RegisterInstance(new DirectoriesConfig(directory.Path, []));
        container.RegisterInstance(ServerMode.Game);
        container.RegisterInstance(TestConfigDocuments.FromToml(directory.Path, toml));

        return container;
    }
}
