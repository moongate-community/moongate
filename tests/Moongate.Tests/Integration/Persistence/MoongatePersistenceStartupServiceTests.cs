using Moongate.Core.Primitives;
using Moongate.Persistence.Services;
using Moongate.Server.Services.Persistence.Internal;
using Moongate.Tests.Support.Persistence;

namespace Moongate.Tests.Integration.Persistence;

public class MoongatePersistenceStartupServiceTests
{
    [Fact]
    public async Task StartAndStopAsync_RegisteredOwner_PersistsDataAndReleasesFiles()
    {
        using var root = new TemporaryPersistenceDirectory();
        var owner = new MoongatePersistenceService(root.Path);
        var items = owner.Register<TestEntity>("items");
        var startup = new MoongatePersistenceStartupService(owner);

        await startup.StartAsync();
        await items.UpsertAsync(new TestEntity { Id = new Serial(7), Name = "saved" });
        await startup.StopAsync();
        await startup.StopAsync();

        await using var reopenedOwner = new MoongatePersistenceService(root.Path);
        var reopenedItems = reopenedOwner.Register<TestEntity>("items");
        await reopenedOwner.InitializeAsync();

        Assert.Equal("saved", reopenedItems.GetById(new Serial(7))?.Name);
    }
}
