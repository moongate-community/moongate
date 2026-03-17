namespace Moongate.Server.Data.Internal.Interaction;

internal sealed record PendingVendorBuyEntry(string ItemTemplateId, int Price, int Stock);
