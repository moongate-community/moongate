namespace Moongate.Server.Data.Internal.Scripting;

/// <summary>
/// Describes a custom context menu entry exposed by an NPC brain script.
/// </summary>
public sealed record LuaBrainContextMenuEntry(string Key, int ClilocId);
