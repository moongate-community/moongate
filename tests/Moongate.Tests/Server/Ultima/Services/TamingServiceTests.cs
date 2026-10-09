using Moongate.Server.Ultima.Data.Taming;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Loaders;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class TamingServiceTests
{
    private readonly TamingService _service = new(
        new StubDataLoaderService().With(
            new TamingCreature { Template = "horse", MinSkill = 29.1, Slots = 1 },
            new TamingCreature { Template = "drake", MinSkill = 90, Slots = 3 }
        )
    );

    [Fact]
    public void TryGet_ATamableCreature_GivesItsData()
    {
        Assert.True(_service.TryGet("drake", out var creature));

        Assert.Equal((90.0, 3), (creature.MinSkill, creature.Slots));
        Assert.Equal(2, _service.Count);
    }

    [Fact]
    public void TryGet_ACreatureWithNoEntry_IsNotTamable()
    {
        Assert.False(_service.TryGet("orc", out _));
    }
}
