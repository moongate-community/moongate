namespace Moongate.Server.Abstractions.Interfaces.Localization;

/// <summary>
/// The client's own localized string table, for the server-side surfaces that need to show a name
/// the shard deliberately does not store — an admin list, a Lua field, a log line. What goes to the
/// game client is the cliloc number itself, not this text.
/// </summary>
public interface IClilocService
{
    /// <summary>
    /// The text of <paramref name="cliloc" /> in the shard's configured language, or null when the
    /// table does not describe it or was never loaded.
    /// </summary>
    string? Text(int cliloc);
}
