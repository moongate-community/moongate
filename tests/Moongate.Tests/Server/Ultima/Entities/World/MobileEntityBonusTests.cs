using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Tests.Server.Ultima.Entities.World;

public sealed class MobileEntityBonusTests
{
    [Fact]
    public void APlayersBonuses_RaiseItsMaximums_AnNpcsKeepTheirs()
    {
        var player = new MobileEntity { Id = new Serial(2), AccountId = new Serial(42), HitsMax = 50, StaminaMax = 40 };
        var orc = new MobileEntity { Id = new Serial(0x100), TemplateId = "orc", HitsMax = 50, StaminaMax = 40 };

        foreach (var mobile in new[] { player, orc })
        {
            mobile.StrengthBonus = 10;
            mobile.DexterityBonus = 5;
        }

        Assert.Equal((60, 45), (player.EffectiveHitsMax, player.EffectiveStaminaMax));
        Assert.Equal((50, 40), (orc.EffectiveHitsMax, orc.EffectiveStaminaMax));
    }

    [Fact]
    public void ASnapshot_LeavesTheBonusesAndWhatTheyHeldAboveTheMaximums_Out()
    {
        var player = new MobileEntity
        {
            Id = new Serial(2), AccountId = new Serial(42), HitsMax = 50, StaminaMax = 40, StrengthBonus = 10,
            DexterityBonus = 5, Hits = 60, Stamina = 45
        };

        var saved = player.Snapshot();

        Assert.Equal((0, 0, 50, 40), (saved.StrengthBonus, saved.DexterityBonus, saved.Hits, saved.Stamina));
        Assert.Equal((60, 45), (player.Hits, player.Stamina));
    }

    [Fact]
    public void ASnapshotOfAnNpc_LeavesItsPoisonOut_ForNothingResumesIt()
    {
        var orc = new MobileEntity { Id = new Serial(0x100), TemplateId = "orc" };
        orc.SetProp("poison.level", 2L);
        orc.SetProp("vega.mood", "calm");

        var saved = orc.Snapshot();

        Assert.False(saved.TryGetProp<long>("poison.level", out _));
        Assert.Equal("calm", saved.GetProp<string>("vega.mood"));
        Assert.True(orc.TryGetProp<long>("poison.level", out _));
    }
}
