using DryIoc;
using Moongate.Core.Extensions.Container;

namespace Moongate.Tests.Core.Extensions.Container;

public sealed class AddTypedListMethodExtensionTests
{
    [Fact]
    public void AddToRegisterTypedList_AppendsToExistingListAndKeepsTypesSeparate()
    {
        using var container = new DryIoc.Container();
        var original = new List<string> { "first" };
        container.RegisterInstance(original);

        var result = container.AddToRegisterTypedList("second").AddToRegisterTypedList(42);

        Assert.Same(container, result);
        Assert.Same(original, container.Resolve<List<string>>());
        Assert.Equal(new[] { "first", "second" }, original);
        Assert.Equal(new[] { 42 }, container.Resolve<List<int>>());
    }

    [Fact]
    public void AddToRegisterTypedList_RejectsNullBeforeRegisteringAnything()
    {
        using var container = new DryIoc.Container();
        IContainer missing = null!;

        Assert.Throws<ArgumentNullException>(() => missing.AddToRegisterTypedList("test"));
        Assert.Throws<ArgumentNullException>(() => container.AddToRegisterTypedList((string)null!));
        Assert.False(container.IsRegistered<List<string>>());
    }
}
