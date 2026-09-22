using DryIoc;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Loaders;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class DataLoaderServiceTests
{
    [Fact]
    public async Task StartAsync_WithNoLoadersRegistered_DoesNotThrow()
    {
        using var container = new Container();
        container.Register<DataLoaderService>(Reuse.Singleton);

        await container.Resolve<DataLoaderService>().StartAsync();
    }

    [Fact]
    public async Task StartAsync_RunsTheRegisteredLoader_AndMakesItsEntitiesAvailableByType()
    {
        using var container = new Container();
        container.RegisterInstance<IReadOnlyList<string>>(["orc", "ratman"]);
        container.RegisterInstance(new List<string>());
        container.AddUltimaDataLoader<FakeDataLoader<string>, string>();
        container.Register<DataLoaderService>(Reuse.Singleton);
        var service = container.Resolve<DataLoaderService>();

        await service.StartAsync();

        Assert.Equal(["orc", "ratman"], service.GetEntities<string>());
    }

    [Fact]
    public async Task StartAsync_RunsLoadersInAscendingPriorityOrder()
    {
        using var container = new Container();
        var callLog = new List<string>();
        container.RegisterInstance(callLog);
        container.RegisterInstance<IReadOnlyList<int>>([1]);
        container.RegisterInstance<IReadOnlyList<string>>(["a"]);
        container.AddUltimaDataLoader<FakeDataLoader<string>, string>(priority: 10);
        container.AddUltimaDataLoader<FakeDataLoader<int>, int>(priority: -10);
        container.Register<DataLoaderService>(Reuse.Singleton);

        await container.Resolve<DataLoaderService>().StartAsync();

        Assert.Equal(["Int32.Initialize", "Int32.Load", "String.Initialize", "String.Load"], callLog);
    }

    [Fact]
    public void GetEntities_WithNoLoaderRegisteredForTheType_ThrowsNamingTheType()
    {
        using var container = new Container();
        container.Register<DataLoaderService>(Reuse.Singleton);
        var service = container.Resolve<DataLoaderService>();

        var exception = Assert.Throws<InvalidOperationException>(() => service.GetEntities<string>());

        Assert.Contains("String", exception.Message, StringComparison.Ordinal);
    }
}
