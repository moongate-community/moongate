using Moongate.UO.Data.Ids;

namespace Moongate.Server.Data.Internal.Interaction;

internal sealed class PendingVendorBuyState
{
    public PendingVendorBuyState(Serial vendorSerial, Serial buyPackSerial, Serial resalePackSerial)
    {
        VendorSerial = vendorSerial;
        BuyPackSerial = buyPackSerial;
        ResalePackSerial = resalePackSerial;
    }

    public Serial BuyPackSerial { get; }
    public Dictionary<Serial, PendingVendorBuyEntry> Entries { get; } = [];
    public Serial ResalePackSerial { get; }
    public Serial VendorSerial { get; }
}
