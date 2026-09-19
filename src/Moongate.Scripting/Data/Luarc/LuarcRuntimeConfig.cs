namespace Moongate.Scripting.Data.Luarc;

/// <summary>The <c>runtime</c> section of <c>.luarc.json</c>: which Lua version the language server should target.</summary>
public sealed class LuarcRuntimeConfig
{
    /// <summary>Gets the Lua version string the language server should assume, e.g. "Lua 5.2".</summary>
    public string Version { get; init; } = "Lua 5.2";
}
