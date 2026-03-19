using Moongate.UO.Data.Ids;

namespace Moongate.Server.Data.Internal.Interaction;

internal sealed record PendingVendorSellEntry(Serial ItemSerial, int Price, int Stock, string ItemTemplateId);
