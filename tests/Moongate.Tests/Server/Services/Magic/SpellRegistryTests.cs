using Moongate.Server.Data.Magic;
using Moongate.Server.Interfaces.Services.Magic;
using Moongate.Server.Services.Magic;
using Moongate.Server.Types.Magic;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Tests.Server.Services.Magic;

[TestFixture]
public sealed class SpellRegistryTests
{
    private SpellRegistry _registry = null!;

    [SetUp]
    public void SetUp()
    {
        _registry = new SpellRegistry();
    }

    [Test]
    public void Register_ThenGet_ReturnsSpell()
    {
        var spell = new StubSpell(4);

        _registry.Register(spell);

        Assert.That(_registry.Get(4), Is.SameAs(spell));
    }

    [Test]
    public void Get_UnknownId_ReturnsNull()
    {
        Assert.That(_registry.Get(999), Is.Null);
    }

    [Test]
    public void Register_Duplicate_ThrowsInvalidOperationException()
    {
        _registry.Register(new StubSpell(1));

        Assert.Throws<InvalidOperationException>(() => _registry.Register(new StubSpell(1)));
    }

    private sealed class StubSpell : ISpell
    {
        public StubSpell(int spellId)
        {
            SpellId = spellId;
        }

        public int SpellId { get; }

        public SpellbookType SpellbookType => SpellbookType.Regular;

        public SpellInfo Info { get; } = new("Test", "An", [], []);

        public SpellTargetingType Targeting => SpellTargetingType.None;

        public int ManaCost => 4;

        public TimeSpan CastDelay => TimeSpan.FromSeconds(1);

        public double MinSkill => 0;

        public double MaxSkill => 60;

        public void ApplyEffect(UOMobileEntity caster, UOMobileEntity? target)
        {
            _ = caster;
            _ = target;
        }
    }
}
