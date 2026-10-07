using Moongate.Core.Primitives;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Data.Death;

/// <summary>
///     An item drawn on a corpse: what it is and the layer who died wore it on.
/// </summary>
public readonly record struct CorpseWornItem(LayerType Layer, Serial Serial);
