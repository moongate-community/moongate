using Moongate.Server.Modules;

namespace Moongate.Tests.Server.Modules;

public sealed class TimeModuleTests
{
    [Test]
    public void NowMs_ShouldBeMonotonicNonDecreasing()
    {
        var module = new TimeModule();

        var first = module.NowMs();
        var second = module.NowMs();

        Assert.That(second, Is.GreaterThanOrEqualTo(first));
    }
}
