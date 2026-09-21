using Moongate.Scripting.Data.Config;

namespace Moongate.Server.Data.Config.Sections;

/// <summary>TOML settings for the Lua scripting engine.</summary>
public sealed class ScriptingConfig
{
    /// <summary>Gets or sets the file executed after the prelude, relative to the scripts directory.</summary>
    public string BootstrapFile { get; set; } = "init.lua";

    /// <summary>Gets or sets the instructions one resume may execute before it is aborted.</summary>
    public int MaxInstructionsPerResume { get; set; } = 150_000;

    /// <summary>Gets or sets the instructions one top-level chunk (the prelude, the bootstrap file, a file loaded by a reload) may execute before it is aborted.</summary>
    public int MaxInstructionsPerChunk { get; set; } = 10_000_000;

    /// <summary>Gets or sets how often, in instructions, the budget hook runs.</summary>
    public int HookInterval { get; set; } = 1_000;

    /// <summary>Gets or sets whether .luarc.json and definitions.lua are written at startup.</summary>
    public bool WriteDefinitions { get; set; } = true;

    /// <summary>Gets or sets the largest string, in characters, that string.rep may build in one call.</summary>
    public int MaxStringLength { get; set; } = 16 * 1024 * 1024;

    /// <summary>Validates the section before server services begin startup: a bootstrap file name, positive budgets, and a hook interval within both budgets.</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(BootstrapFile))
        {
            throw new InvalidOperationException("The scripting bootstrap_file cannot be blank.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxInstructionsPerResume, nameof(MaxInstructionsPerResume));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxInstructionsPerChunk, nameof(MaxInstructionsPerChunk));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(HookInterval, nameof(HookInterval));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxStringLength, nameof(MaxStringLength));

        if (HookInterval > MaxInstructionsPerResume || HookInterval > MaxInstructionsPerChunk)
        {
            throw new ArgumentOutOfRangeException(
                nameof(HookInterval),
                "The scripting hook_interval cannot exceed either instruction budget."
            );
        }
    }

    /// <summary>Maps validated TOML settings to immutable engine options, using the given scripts directory.</summary>
    public ScriptEngineOptions ToOptions(string scriptsDirectory)
    {
        var options = new ScriptEngineOptions
        {
            ScriptsDirectory = scriptsDirectory,
            BootstrapFile = BootstrapFile,
            MaxInstructionsPerResume = MaxInstructionsPerResume,
            MaxInstructionsPerChunk = MaxInstructionsPerChunk,
            HookInterval = HookInterval,
            WriteDefinitions = WriteDefinitions,
            MaxStringLength = MaxStringLength
        };
        options.Validate();

        return options;
    }
}
