using Moongate.Server.Types.Magic;

namespace Moongate.Tests.Server.Types.Magic;

[TestFixture]
public sealed class SpellCircleTypeTests
{
    [Test]
    public void SpellCircleType_HasEightCirclesPlusNone()
    {
        Assert.That(Enum.GetValues<SpellCircleType>().Length, Is.EqualTo(9));
    }

    [Test]
    public void SpellStateType_None_HasValueZero()
    {
        Assert.That((int)SpellStateType.None, Is.EqualTo(0));
    }

    [Test]
    public void SpellStateType_HasThreeStates()
    {
        Assert.That(Enum.GetValues<SpellStateType>().Length, Is.EqualTo(3));
    }
}
