namespace Moongate.Scripting.Data.Luarc;

/// <summary>The <c>workspace</c> section of <c>.luarc.json</c>: where the language server finds declaration files.</summary>
public sealed class LuarcWorkspaceConfig
{
    /// <summary>Gets the paths, relative to the scripts directory, the language server loads as library declarations.</summary>
    public IReadOnlyList<string> Library { get; init; } = ["definitions.lua"];

    /// <summary>Gets whether the language server should look for third-party library configs it does not know about.</summary>
    public bool CheckThirdParty { get; init; }
}
