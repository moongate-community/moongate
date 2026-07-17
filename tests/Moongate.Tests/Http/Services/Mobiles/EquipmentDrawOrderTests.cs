using Moongate.Http.Plugin.Data.Mobiles;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Http.Services.Mobiles;

public class EquipmentDrawOrderTests
{
    [Fact]
    public void CloakDrawsBehindTheBody_HairAboveClothes_WeaponsOnTop()
    {
        Assert.True(EquipmentDrawOrder.Priority(LayerType.Cloak) < EquipmentDrawOrder.BodyPriority);
        Assert.True(EquipmentDrawOrder.Priority(LayerType.Hair) > EquipmentDrawOrder.Priority(LayerType.OuterTorso));
        Assert.True(EquipmentDrawOrder.Priority(LayerType.TwoHanded) > EquipmentDrawOrder.Priority(LayerType.Helm));
    }

    [Fact]
    public void NonDrawableLayers_AreSkipped()
    {
        Assert.Equal(EquipmentDrawOrder.Skip, EquipmentDrawOrder.Priority(LayerType.Backpack));
        Assert.Equal(EquipmentDrawOrder.Skip, EquipmentDrawOrder.Priority(LayerType.Mount));
        Assert.Equal(EquipmentDrawOrder.Skip, EquipmentDrawOrder.Priority(LayerType.None));
    }
}
