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
    [Fact]
    public async Task Apply_RejectsDirectSchemaSynchronization()
    {
        await using var fixture = await HostPersistenceFixture.CreateAsync(false);
        fixture.RegisterEntity();
        using var output = new StringWriter();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(
                        () =>
                            PersistenceSchemaCommand.RunAsync(
                                fixture.Container,
                                PersistenceSchemaMode.Apply,
                                output,
                                CancellationToken.None
                            )
                    );
        Assert.Contains("Moongate.MigrationRunner", error.Message);
        Assert.False(await fixture.Database.ScalarAsync<bool>("SELECT to_regclass('host_test.items') IS NOT NULL"));
    }

    [Fact]
    public async Task Generate_WritesReviewableSqlWithoutExecutingOrOverwriting()
    {
        await using var fixture = await HostPersistenceFixture.CreateAsync(false);
        fixture.RegisterEntity();
        var directory = Path.Combine(Path.GetTempPath(), $"moongate_draft_{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "0001_create.sql");

        try
        {
            using var output = new StringWriter();
            await PersistenceSchemaCommand.RunAsync(
                fixture.Container,
                PersistenceSchemaMode.Generate,
                output,
                CancellationToken.None,
                path,
                "world"
            );
            var sql = await File.ReadAllTextAsync(path);
            Assert.Contains("CREATE TABLE", sql, StringComparison.OrdinalIgnoreCase);
            Assert.False(await fixture.Database.ScalarAsync<bool>("SELECT to_regclass('host_test.items') IS NOT NULL"));
            await Assert.ThrowsAsync<IOException>(
                () => PersistenceSchemaCommand.RunAsync(
                    fixture.Container,
                    PersistenceSchemaMode.Generate,
                    output,
                    CancellationToken.None,
                    path,
                    "world"
                )
            );
            Assert.Equal(sql, await File.ReadAllTextAsync(path));
            await fixture.Database.ExecuteAsync(sql);
            Assert.True(await fixture.Database.ScalarAsync<bool>("SELECT to_regclass('host_test.items') IS NOT NULL"));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    [Fact]
    public async Task RunAsync_PluginRegistrations_PreparesSchemaWithoutStartingAnyService()
    {
        await using var fixture = await HostPersistenceFixture.CreateAsync(false);
        var loader = new PersistencePluginLoader(fixture.Container);
        fixture.Container.RegisterInstance<IPluginLoaderService>(loader);
        fixture.Container.RegisterMoongateService<CallbackStartupService>(
            () => throw new IOException("service must not resolve"),
            -2000
        );
        using var output = new StringWriter();
        await PersistenceSchemaCommand.RunAsync(
            fixture.Container,
            PersistenceSchemaMode.Preview,
            output,
            CancellationToken.None
        );
        Assert.Equal(1, loader.Loads);
        Assert.Contains("host.test", output.ToString());
        var exists = await fixture.Database.ScalarAsync<bool>("SELECT to_regclass('host_test.items') IS NOT NULL");
        Assert.False(exists);
    }
}
