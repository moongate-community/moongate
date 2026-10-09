using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Data.Internal.Vendors;

namespace Moongate.Server.Ultima.Data.Vendors;

/// <summary>
///     The session values of the vendors.
/// </summary>
public static class VendorSessionKeys
{
    public static readonly SessionKey<VendorWindow?> Window = new("VendorWindow");

    public static readonly SessionKey<VendorSellWindow?> SellWindow = new("VendorSellWindow");
}
