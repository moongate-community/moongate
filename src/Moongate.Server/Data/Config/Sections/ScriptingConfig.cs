using Moongate.Scripting.Data.Config;

namespace Moongate.Server.Data.Config.Sections;

/// <summary>TOML settings for the Lua scripting engine.</summary>
public sealed class ScriptingConfig
{
    /// <summary>Gets or sets the file executed after the prelude, relative to the scripts directory.</summary>
    public string BootstrapFile { get; set; } = "init.lua";

    /// <summary>Gets or sets the instructions one resume may execute before it is aborted.</summary>
    public int MaxInstructionsPerResume { get; set; } = 150_000;

    /// <summary>Gets or sets how often, in instructions, the budget hook runs.</summary>
    public int HookInterval { get; set; } = 1_000;

    /// <summary>Gets or sets whether .luarc.json and definitions.lua are written at startup.</summary>
    public bool WriteDefinitions { get; set; } = true;

    /// <summary>Maps validated TOML settings to immutable engine options, using the given scripts directory.</summary>
    public ScriptEngineOptions ToOptions(string scriptsDirectory)
    {
        var options = new ScriptEngineOptions
        {
            ScriptsDirectory = scriptsDirectory,
            BootstrapFile = BootstrapFile,
            MaxInstructionsPerResume = MaxInstructionsPerResume,
            HookInterval = HookInterval,
            WriteDefinitions = WriteDefinitions
        };
        options.Validate();

        return options;
    }
}
