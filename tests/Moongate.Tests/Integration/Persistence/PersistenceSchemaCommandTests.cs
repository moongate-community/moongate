using DryIoc;
using Moongate.Server.Bootstrap.Internal;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Types.Persistence;
using Moongate.Tests.Support.Server;
using Moongate.Tests.TestSupport.Persistence;

namespace Moongate.Tests.Integration.Persistence;

public sealed class PersistenceSchemaCommandTests
{
    [Theory, InlineData(PersistenceSchemaMode.Preview), InlineData(PersistenceSchemaMode.Apply)]
    public async Task RunAsync_PluginRegistrations_PreparesSchemaWithoutStartingAnyService(PersistenceSchemaMode mode)
    {
        await using var fixture = await HostPersistenceFixture.CreateAsync(autoSync: false);
        var loader = new PersistencePluginLoader(fixture.Container);
        fixture.Container.RegisterInstance<IPluginLoaderService>(loader);
        fixture.Container.RegisterMoongateService<CallbackStartupService>(
            () => throw new IOException("service must not resolve"),
            -2000
        );
        using var output = new StringWriter();
        await PersistenceSchemaCommand.RunAsync(fixture.Container, mode, output, CancellationToken.None);
        Assert.Equal(1, loader.Loads);
        Assert.Contains("host.test", output.ToString());
        var exists = await fixture.Database.ScalarAsync<bool>("SELECT to_regclass('host_test.items') IS NOT NULL");
        Assert.Equal(mode == PersistenceSchemaMode.Apply, exists);
    }
}
