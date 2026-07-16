using Moongate.Ultima.Types;

namespace Moongate.Server.Data.Internal.Mobiles;

/// <summary>An equipment entry with its layer and hue already resolved, ready to create and equip.</summary>
public sealed record ResolvedEquipment(string ItemTemplateId, LayerType Layer, ushort Hue);
