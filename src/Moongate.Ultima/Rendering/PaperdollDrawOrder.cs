using Moongate.Ultima.Types;

namespace Moongate.Ultima.Rendering;

/// <summary>
/// Paint order for paperdoll layers: lower priority is drawn first (covered by later
/// layers). Cloak sits right above the body; helm covers hair.
/// </summary>
public static class PaperdollDrawOrder
{
    public const int BackgroundPriority = 0;
    public const int BodyPriority = 10;

    public static int Priority(LayerType layer)
    {
        return layer switch
        {
            LayerType.Cloak => 11,
            LayerType.Shoes => 12,
            LayerType.Pants => 13,
            LayerType.InnerLegs => 14,
            LayerType.Shirt => 15,
            LayerType.InnerTorso => 16,
            LayerType.Ring => 17,
            LayerType.Talisman => 18,
            LayerType.Bracelet => 19,
            LayerType.Gloves => 20,
            LayerType.OuterLegs => 21,
            LayerType.MiddleTorso => 22,
            LayerType.Waist => 23,
            LayerType.OuterTorso => 24,
            LayerType.Neck => 25,
            LayerType.Hair => 26,
            LayerType.FacialHair => 27,
            LayerType.Earrings => 28,
            LayerType.Helm => 29,
            LayerType.Arms => 30,
            LayerType.TwoHanded => 31,
            LayerType.OneHanded => 32,
            _ => 33
        };
    }
}
