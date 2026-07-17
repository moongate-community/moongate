using Moongate.Ultima.Types;

namespace Moongate.Http.Plugin.Data.Mobiles;

/// <summary>
/// Front-facing draw priority for mobile figure layers (lower draws first/behind). The body sits at
/// <see cref="BodyPriority" />; non-drawable layers return <see cref="Skip" />. Tuned for a catalog
/// thumbnail (single facing); per-direction order is a later refinement.
/// </summary>
public static class EquipmentDrawOrder
{
    public const int Skip = int.MaxValue;

    public const int BodyPriority = 5;

    public static int Priority(LayerType layer)
        => layer switch
        {
            LayerType.Cloak => 0,
            LayerType.InnerLegs => 10,
            LayerType.Pants => 11,
            LayerType.Shoes => 12,
            LayerType.Shirt => 13,
            LayerType.InnerTorso => 14,
            LayerType.Arms => 15,
            LayerType.MiddleTorso => 16,
            LayerType.Gloves => 17,
            LayerType.Neck => 18,
            LayerType.Waist => 19,
            LayerType.OuterLegs => 20,
            LayerType.OuterTorso => 21,
            LayerType.Hair => 25,
            LayerType.FacialHair => 26,
            LayerType.Helm => 30,
            LayerType.OneHanded => 35,
            LayerType.TwoHanded => 36,
            _ => Skip
        };
}
