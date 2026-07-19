using Moongate.Core.Primitives;
using Moongate.Ultima.Types;
using Moongate.UO.Data.Hues;

namespace Moongate.Network.Data;

/// <summary>
/// One equipped item (or hair/facial-hair pseudo-item) as it appears in a mobile-incoming (0x78)
/// packet: the serial, graphic, paperdoll layer, and hue the client should draw.
/// </summary>
public readonly record struct MobileIncomingItem(Serial Serial, ushort ItemId, LayerType Layer, Hue Hue);
