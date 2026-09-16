using DryIoc;
using Moongate.Core.Primitives;
using Moongate.Persistence.DataAccess;
using Moongate.Persistence.Extensions;
using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Services;
using Moongate.Tests.Support.Persistence;

namespace Moongate.Tests.Persistence.Extensions;

public sealed class ContainerPersistenceExtensionsTests
{
    [Fact]
    public async Task Registration_WithLiveSource_SaveAllUsesCurrentEntitiesThroughSameDataAccess()
    {
        using var root = new TemporaryPersistenceDirectory();
        using var container = new Container();
        var entity = new TestEntity { Id = new Serial(1), Name = "before" };
        var result = container.RegisterMoongatePersistence(root.Path)
            .RegisterDataAccess<TestEntity>("items", () => new[] { entity });
        await using var owner = container.Resolve<MoongatePersistenceService>();
        await owner.InitializeAsync();
        entity.Name = "after";

        await owner.SaveAllAsync();

        Assert.Same(container, result);
        Assert.Same(container.Resolve<DataAccess<TestEntity>>(), container.Resolve<IDataAccess<TestEntity>>());
        Assert.Equal("after", container.Resolve<IDataAccess<TestEntity>>().GetById(entity.Id)!.Name);
    }

    [Fact]
    public void Registration_ReturnsContainerAndResolvesSingletonIdentitiesWithoutCreatingFiles()
    {
        using var root = new TemporaryPersistenceDirectory();
        using var container = new Container();

        var result = container.RegisterMoongatePersistence(root.Path)
            .RegisterDataAccess<TestEntity>("items");

        Assert.Same(container, result);
        Assert.Same(container.Resolve<MoongatePersistenceService>(), container.Resolve<MoongatePersistenceService>());
        Assert.Same(container.Resolve<DataAccess<TestEntity>>(), container.Resolve<IDataAccess<TestEntity>>());
        Assert.Empty(Directory.GetFiles(root.Path));
    }

    [Fact]
    public async Task RegisteredDataAccess_OwnerControlsItsDisposal()
    {
        using var root = new TemporaryPersistenceDirectory();
        var container = new Container();
        container.RegisterMoongatePersistence(root.Path).RegisterDataAccess<TestEntity>("items");
        var owner = container.Resolve<MoongatePersistenceService>();
        var access = container.Resolve<IDataAccess<TestEntity>>();
        await owner.InitializeAsync();

        container.Dispose();
        Assert.Empty(access.GetAll());
        await owner.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => access.GetAll());
    }
}
