namespace Moongate.Server.Ultima.Data.ContextMenus;

/// <summary>
///     One entry of a context menu.
/// </summary>
/// <param name="Cliloc">
///     The text of the client the entry shows.
/// </param>
/// <param name="Range">
///     How many tiles away the player may choose it.
/// </param>
/// <param name="Enabled">
///     Whether it can be chosen at all; one that cannot is shown greyed out.
/// </param>
/// <param name="ScriptId">
///     The entry's own name in the script that added it; null for an entry of the server.
/// </param>
public sealed record ContextMenuEntry(int Cliloc, int Range, bool Enabled, string? ScriptId);
