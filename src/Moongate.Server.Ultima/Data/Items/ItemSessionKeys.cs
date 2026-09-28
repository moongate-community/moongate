using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Data.Internal.Items;

namespace Moongate.Server.Ultima.Data.Items;

/// <summary>
///     The session values of items.
/// </summary>
public static class ItemSessionKeys
{
    public static readonly SessionKey<HeldItem?> Held = new("HeldItem");
}
