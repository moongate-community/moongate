using Moongate.Server.Ultima.Types.Combat;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Services.Internal;

/// <summary>
///     Which part of the body each layer of armor covers.
/// </summary>
internal static class CombatZones
{
    /// <summary>
    ///     Gets the part of the body a worn layer covers; null for a layer that is no armor (a ring, a shirt, a hand).
    /// </summary>
    public static ArmorZoneType? ZoneOf(LayerType layer)
    {
        return layer switch
        {
            LayerType.Neck                                                       => ArmorZoneType.Neck,
            LayerType.Gloves                                                     => ArmorZoneType.Hands,
            LayerType.Arms                                                       => ArmorZoneType.Arms,
            LayerType.Helm                                                       => ArmorZoneType.Head,
            LayerType.Pants or LayerType.OuterLegs or LayerType.InnerLegs        => ArmorZoneType.Legs,
            LayerType.Shirt or LayerType.InnerTorso or LayerType.MiddleTorso or LayerType.OuterTorso => ArmorZoneType.Chest,
            _                                                                    => null
        };
    }
}
