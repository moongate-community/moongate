using Moongate.UO.Data.Ids;

namespace Moongate.Server.Data.Internal.Interaction;

internal sealed class PendingVendorSellState
{
    public PendingVendorSellState(Serial vendorSerial)
    {
        VendorSerial = vendorSerial;
    }

    public Dictionary<Serial, PendingVendorSellEntry> Entries { get; } = [];
    public Serial VendorSerial { get; }
}
