using Moongate.Plugin.Abstractions.Interfaces;

namespace Moongate.Tests.Server.Plugins;

public class PluginContractSmokeTests
{
    [Test]
    public void PluginAbstractions_ShouldExposeExpectedContractSurface()
    {
        Assert.Multiple(
            () =>
            {
                Assert.That(typeof(IMoongatePlugin), Is.Not.Null);
                Assert.That(typeof(IMoongatePluginContext), Is.Not.Null);
                Assert.That(typeof(IMoongatePluginRuntimeContext), Is.Not.Null);
                Assert.That(typeof(IMoongatePluginServiceResolver), Is.Not.Null);
            }
        );
    }
}
