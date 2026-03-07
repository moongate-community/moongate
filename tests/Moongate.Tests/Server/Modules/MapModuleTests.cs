using Moongate.Server.Modules;

namespace Moongate.Tests.Server.Modules;

public sealed class MapModuleTests
{
    [Test]
    public void ToId_WhenNameIsKnown_ShouldReturnExpectedMapId()
    {
        var module = new MapModule();

        Assert.Multiple(
            () =>
            {
                Assert.That(module.ToId("felucca"), Is.EqualTo(0));
                Assert.That(module.ToId("trammel"), Is.EqualTo(1));
                Assert.That(module.ToId("termur"), Is.EqualTo(5));
                Assert.That(module.ToId("internal"), Is.EqualTo(0x7F));
            }
        );
    }

    [Test]
    public void ToId_WhenNameIsUnknown_ShouldReturnFallback()
    {
        var module = new MapModule();

        var result = module.ToId("unknown_map", 42);

        Assert.That(result, Is.EqualTo(42));
    }

    [Test]
    public void ToId_WhenValueIsNumeric_ShouldReturnIntegerValue()
    {
        var module = new MapModule();

        Assert.Multiple(
            () =>
            {
                Assert.That(module.ToId(3), Is.EqualTo(3));
                Assert.That(module.ToId(4.9), Is.EqualTo(4));
                Assert.That(module.ToId("5"), Is.EqualTo(5));
            }
        );
    }
}
