using Moongate.Ultima.Types;

namespace Moongate.Http.Plugin.Data.Mobiles;

/// <summary>
/// Paperdoll draw priority (lower draws first/behind). Background and body sit below zero as a
/// backdrop — unlike <see cref="EquipmentDrawOrder" />'s figure, where the body is mid-stack. Layers
/// the paperdoll never shows return <see cref="Skip" />.
/// </summary>
public static class PaperdollDrawOrder
{
    public const int Skip = int.MaxValue;

    public const int BackgroundPriority = -100;

    public const int BodyPriority = -50;

    public static int Priority(LayerType layer)
        => layer switch
        {
            LayerType.Cloak       => 10,
            LayerType.Shirt       => 20,
            LayerType.Pants       => 30,
            LayerType.InnerLegs   => 40,
            LayerType.Shoes       => 50,
            LayerType.InnerTorso  => 60,
            LayerType.Arms        => 70,
            LayerType.MiddleTorso => 80,
            LayerType.OuterLegs   => 90,
            LayerType.Neck        => 100,
            LayerType.Waist       => 110,
            LayerType.OuterTorso  => 120,
            LayerType.Gloves      => 130,
            LayerType.Ring        => 140,
            LayerType.Talisman    => 150,
            LayerType.Bracelet    => 160,
            LayerType.Hair        => 170,
            LayerType.FacialHair  => 180,
            LayerType.Earrings    => 190,
            LayerType.Helm        => 200,
            LayerType.OneHanded   => 210,
            LayerType.TwoHanded   => 220,
            _                     => Skip
        };
}
