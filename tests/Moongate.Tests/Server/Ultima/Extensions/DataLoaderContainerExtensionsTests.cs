using DryIoc;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Tests.TestSupport.Ultima.Loaders;

namespace Moongate.Tests.Server.Ultima.Extensions;

public sealed class DataLoaderContainerExtensionsTests
{
    [Fact]
    public void AddUltimaDataLoader_RegistersTheLoaderAsASingleton_SharedWithTheInterface()
    {
        using var container = CreateContainer(["orc"]);

        container.AddUltimaDataLoader<FakeDataLoader<string>, string>();

        Assert.Same(
            container.Resolve<FakeDataLoader<string>>(),
            container.Resolve<IDataLoader<string>>()
        );
    }

    [Fact]
    public void AddUltimaDataLoader_ReturnsTheSameContainerForChaining()
    {
        using var container = CreateContainer(["orc"]);

        Assert.Same(container, container.AddUltimaDataLoader<FakeDataLoader<string>, string>());
    }

    private static Container CreateContainer(IReadOnlyList<string> entities)
    {
        var container = new Container();

        container.RegisterInstance(entities);
        container.RegisterInstance(new List<string>());

        return container;
    }
}
