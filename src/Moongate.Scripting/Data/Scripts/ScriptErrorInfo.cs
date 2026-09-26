namespace Moongate.Scripting.Data.Scripts;

/// <summary>
///     Where and why a script failed.
/// </summary>
/// <param name="File">
///     Path relative to the scripts directory, or the chunk name when the code did not come from a file.
/// </param>
/// <param name="Line">
///     One-based line, or 0 when unknown.
/// </param>
/// <param name="Message">
///     The Lua error message without the position prefix.
/// </param>
/// <param name="Traceback">
///     The Lua traceback when available, otherwise null.
/// </param>
public sealed record ScriptErrorInfo(string File, int Line, string Message, string? Traceback);
