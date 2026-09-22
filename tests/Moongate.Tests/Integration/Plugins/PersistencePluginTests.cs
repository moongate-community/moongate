using System.Runtime.Loader;
using DryIoc;
using FreeSql.DataAnnotations;
using Moongate.Persistence.Extensions;
using Moongate.Persistence.Services;
using Moongate.Persistence.Types.Persistence;
using Moongate.Server.Services.Plugins;
using Moongate.Tests.TestSupport.Persistence;
using Moongate.Tests.TestSupport.Plugins;
using Npgsql;

namespace Moongate.Tests.Integration.Plugins;

[Collection(PostgresTestCollection.Name)]
public sealed class PersistencePluginTests
{
    [Fact]
    public async Task InitializeAsync_TwoRealPluginSchemasCollide_RejectsBeforeAnyDdl()
    {
        using var files = new PluginDirectoryFixture();
        files.Deploy("PersistencePlugin");
        files.Deploy("CollisionPlugin");
        await using var fixture = await HostPersistenceFixture.CreateAsync();
        using var loader = new PluginLoaderService(fixture.Container, files.Directories);
        loader.LoadPlugins();
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Owner.InitializeAsync());
        Assert.False(
            await fixture.Database.ScalarAsync<bool>(
                "SELECT EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = 'fixture_data')"
            )
        );
    }

    [Fact]
    public async Task LoadPlugins_IncompatiblePrivateContract_FailsClearlyWithoutPrivateFallback()
    {
        using var files = new PluginDirectoryFixture();
        files.Deploy("IncompatiblePersistencePlugin");
        using var container = new Container();
        using var loader = new PluginLoaderService(container, files.Directories);
        var error = Record.Exception(loader.LoadPlugins);
        Assert.NotNull(error);
        Assert.Contains("Moongate.Persistence", error.ToString());
        Assert.Contains("99.0.0.0", error.ToString());
        await Task.CompletedTask;
    }

    [Fact]
    public async Task LoadPlugins_RealBundle_SharesContractsAndRegistersWithoutIo()
    {
        using var files = new PluginDirectoryFixture();
        var bundle = files.Deploy("PersistencePlugin");
        Assert.True(File.Exists(Path.Combine(bundle, "Moongate.Persistence.dll")));
        using var container = new Container();
        container.RegisterMoongatePersistence(
            new(
                [
                    new(
                        PersistenceDatabaseTarget.Realm,
                        () => throw new IOException("unexpected database resolution")
                    )
                ]
            )
        );
        await using var owner = container.Resolve<MoongatePersistenceService>();
        using var loader = new PluginLoaderService(container, files.Directories);
        loader.LoadPlugins();
        var types = container.Resolve<Type[]>();
        Assert.Same(typeof(MoongatePersistenceService), types[0]);
        Assert.Same(typeof(IFreeSql), types[1]);
        Assert.Same(typeof(TableAttribute), types[2]);
        Assert.Same(typeof(NpgsqlConnection), types[3]);
        Assert.NotSame(AssemblyLoadContext.Default, AssemblyLoadContext.GetLoadContext(types[4].Assembly));
        Assert.Contains(
            "unexpected database resolution",
            (await Assert.ThrowsAsync<IOException>(() => owner.InitializeAsync())).Message
        );
    }
}
