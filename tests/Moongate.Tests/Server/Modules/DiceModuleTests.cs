using Moongate.Server.Modules;

namespace Moongate.Tests.Server.Modules;

public sealed class DiceModuleTests
{
    [Test]
    public void Roll_WithValidExpression_ShouldReturnValueInExpectedRange()
    {
        var module = new DiceModule();

        for (var i = 0; i < 100; i++)
        {
            var value = module.Roll("1d4+2");
            Assert.That(value, Is.InRange(3, 6));
        }
    }

    [Test]
    public void TryRoll_WithInvalidExpression_ShouldReturnFailure()
    {
        var module = new DiceModule();

        var (ok, value) = module.TryRoll("abc");

        Assert.Multiple(
            () =>
            {
                Assert.That(ok, Is.False);
                Assert.That(value, Is.EqualTo(0));
            }
        );
    }

    [Test]
    public void TryRoll_WithValidExpression_ShouldReturnSuccessAndValue()
    {
        var module = new DiceModule();

        var (ok, value) = module.TryRoll("2d1+1");

        Assert.Multiple(
            () =>
            {
                Assert.That(ok, Is.True);
                Assert.That(value, Is.EqualTo(3));
            }
        );
    }
}
