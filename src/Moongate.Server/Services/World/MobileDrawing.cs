using Moongate.Network.Data;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Interfaces.Items;
using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.Ultima.Types;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Services.World;

/// <summary>
/// What a mobile looks like on the wire. Shared because drawing a mobile is not only something its
/// owner's client does: everyone who can see it draws the same equipment.
/// </summary>
public static class MobileDrawing
{
    private const byte FemaleFlag = 0x02;

    /// <summary>
    /// Builds the worn items the client draws on a mobile: its equipment, then hair and facial hair as
    /// pseudo-items. One item per layer wins, as in ModernUO — the client cannot render two things on the
    /// same slot, and hair only goes out if nothing real already claimed its layer (a helm, say).
    /// </summary>
    public static List<MobileIncomingItem> BuildEquipment(
        MobileEntity mobile,
        IItemService items,
        IVirtualSerialService virtualSerials
    )
    {
        var drawn = new List<MobileIncomingItem>();
        var takenLayers = new HashSet<LayerType>();

        foreach (var item in items.GetEquipped(mobile))
        {
            if (item.EquippedLayer is not { } layer || !takenLayers.Add(layer))
            {
                continue;
            }

            drawn.Add(new(item.Id, (ushort)item.ItemId, layer, item.Hue));
        }

        if (mobile.HairStyle != 0 && takenLayers.Add(LayerType.Hair))
        {
            drawn.Add(
                new(
                    virtualSerials.GetOrCreate(mobile.Id, LayerType.Hair),
                    mobile.HairStyle,
                    LayerType.Hair,
                    mobile.HairHue
                )
            );
        }

        if (mobile.FacialHairStyle != 0 && takenLayers.Add(LayerType.FacialHair))
        {
            drawn.Add(
                new(
                    virtualSerials.GetOrCreate(mobile.Id, LayerType.FacialHair),
                    mobile.FacialHairStyle,
                    LayerType.FacialHair,
                    mobile.FacialHairHue
                )
            );
        }

        return drawn;
    }

    /// <summary>The body flags a mobile is drawn with. Only the female bit today.</summary>
    public static byte BuildFlags(MobileEntity mobile)
        => mobile.Gender == GenderType.Female ? FemaleFlag : (byte)0;
}
