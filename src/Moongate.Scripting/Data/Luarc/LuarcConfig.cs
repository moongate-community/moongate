using System.Text.Json;

namespace Moongate.Scripting.Data.Luarc;

/// <summary>
///     The settings written to
///     <c>
///         .luarc.json
///     </c>
///     so the Lua language server matches what the engine binds.
/// </summary>
public sealed class LuarcConfig
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    /// <summary>
    ///     Gets the runtime section: which Lua version the language server should target.
    /// </summary>
    public LuarcRuntimeConfig Runtime { get; init; } = new();

    /// <summary>
    ///     Gets the workspace section: where the language server finds declaration files.
    /// </summary>
    public LuarcWorkspaceConfig Workspace { get; init; } = new();

    /// <summary>
    ///     Gets the diagnostics section: globals the language server must not flag as undefined.
    /// </summary>
    public LuarcDiagnosticsConfig Diagnostics { get; init; } = new();

    /// <summary>
    ///     Serialises to the flat dotted-key form the Lua language server expects.
    /// </summary>
    public string ToJson()
    {
        var flat = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["runtime.version"] = Runtime.Version,
            ["workspace.library"] = Workspace.Library,
            ["workspace.checkThirdParty"] = Workspace.CheckThirdParty,
            ["diagnostics.globals"] = Diagnostics.Globals
        };

        return JsonSerializer.Serialize(flat, SerializerOptions);
    }
}
