using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.ContextMenus;

namespace Moongate.Server.Ultima.Data.Internal.ContextMenus;

/// <summary>
///     The context menu last sent to a session, kept until its choice arrives: the choice is an index into it.
/// </summary>
/// <param name="Target">The mobile or item the menu is of.</param>
/// <param name="Entries">The entries in the order they were sent.</param>
public sealed record SentContextMenu(Serial Target, IReadOnlyList<ContextMenuEntry> Entries);
