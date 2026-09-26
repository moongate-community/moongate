namespace Moongate.Scripting.Data.Luarc;

/// <summary>
///     The
///     <c>
///         diagnostics
///     </c>
///     section of
///     <c>
///         .luarc.json
///     </c>
///     : globals the language server must not flag as undefined.
/// </summary>
public sealed class LuarcDiagnosticsConfig
{
    /// <summary>
    ///     Gets the global names the language server should treat as defined: every bound module, enum and
    ///     <c>
    ///         wait
    ///     </c>
    ///     .
    /// </summary>
    public IReadOnlyList<string> Globals { get; init; } = [];
}
