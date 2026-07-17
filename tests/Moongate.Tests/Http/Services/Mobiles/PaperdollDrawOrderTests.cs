using Moongate.Http.Plugin.Data.Mobiles;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Http.Services.Mobiles;

public class PaperdollDrawOrderTests
{
    [Fact]
    public void BackgroundAndBody_DrawBelowEveryEquipmentLayer()
    {
        Assert.True(PaperdollDrawOrder.BackgroundPriority < PaperdollDrawOrder.BodyPriority);
        Assert.True(PaperdollDrawOrder.BodyPriority < PaperdollDrawOrder.Priority(LayerType.Cloak));
    }

    [Fact]
    public void ClothesDrawBelowHair_HairBelowHelm_HelmBelowWeapons()
    {
        Assert.True(PaperdollDrawOrder.Priority(LayerType.Shirt) < PaperdollDrawOrder.Priority(LayerType.Hair));
        Assert.True(PaperdollDrawOrder.Priority(LayerType.Hair) < PaperdollDrawOrder.Priority(LayerType.Helm));
        Assert.True(PaperdollDrawOrder.Priority(LayerType.Helm) < PaperdollDrawOrder.Priority(LayerType.TwoHanded));
    }

    [Fact]
    public void NonDrawableLayers_AreSkipped()
    {
        Assert.Equal(PaperdollDrawOrder.Skip, PaperdollDrawOrder.Priority(LayerType.Backpack));
        Assert.Equal(PaperdollDrawOrder.Skip, PaperdollDrawOrder.Priority(LayerType.Mount));
        Assert.Equal(PaperdollDrawOrder.Skip, PaperdollDrawOrder.Priority(LayerType.None));
    }
}
