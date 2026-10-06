using Moongate.Server.Core.Data.Sessions;

namespace Moongate.Server.Ultima.Data.Internal.ContextMenus;

/// <summary>
///     The session values of context menus.
/// </summary>
internal static class ContextMenuSessionKeys
{
    public static readonly SessionKey<SentContextMenu?> Sent = new("SentContextMenu");
}
