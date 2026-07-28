using Moongate.Core.Primitives;
using Moongate.Server.Services.Items;

namespace Moongate.Tests.Server.Items;

public class ContainerOpenerRegistryTests
{
    [Fact]
    public void Closed_DropsOnlyThatOpener()
    {
        var registry = new ContainerOpenerRegistry();
        registry.Opened((Serial)10, (Serial)1);
        registry.Opened((Serial)10, (Serial)2);

        registry.Closed((Serial)10, (Serial)1);

        Assert.Equal([(Serial)2], registry.OpenersOf((Serial)10));
    }

    [Fact]
    public void ForgetMobile_DropsItFromEveryContainer()
    {
        var registry = new ContainerOpenerRegistry();
        registry.Opened((Serial)10, (Serial)1);
        registry.Opened((Serial)11, (Serial)1);
        registry.Opened((Serial)11, (Serial)2);

        registry.ForgetMobile((Serial)1);

        Assert.Empty(registry.OpenersOf((Serial)10));
        Assert.Equal([(Serial)2], registry.OpenersOf((Serial)11));
    }

    [Fact]
    public void Opened_RecordsTheOpener()
    {
        var registry = new ContainerOpenerRegistry();

        registry.Opened((Serial)10, (Serial)1);

        Assert.Equal([(Serial)1], registry.OpenersOf((Serial)10));
    }

    [Fact]
    public void Opened_Twice_RecordsTheOpenerOnce()
    {
        var registry = new ContainerOpenerRegistry();

        registry.Opened((Serial)10, (Serial)1);
        registry.Opened((Serial)10, (Serial)1);

        Assert.Single(registry.OpenersOf((Serial)10));
    }

    [Fact]
    public void OpenersOf_UnknownContainer_IsEmpty()
        => Assert.Empty(new ContainerOpenerRegistry().OpenersOf((Serial)10));
}
