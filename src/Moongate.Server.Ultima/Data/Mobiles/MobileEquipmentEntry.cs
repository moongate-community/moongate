using Moongate.Core.Primitives;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Data.Mobiles;

/// <summary>
///     One item a mobile wears, as 0x78 lists it: hair and beard are entries too, with virtual serials.
/// </summary>
/// <param name="Serial">
///     The item's serial, or a virtual serial for hair and beard.
/// </param>
/// <param name="ItemId">
///     The graphic.
/// </param>
/// <param name="Layer">
///     The layer it is worn on.
/// </param>
/// <param name="Hue">
///     Its hue.
/// </param>
public sealed record MobileEquipmentEntry(Serial Serial, int ItemId, LayerType Layer, Hue Hue);
