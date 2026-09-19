namespace Moongate.Scripting.Data.Config;

/// <summary>Everything the script engine needs to start: where the scripts live, which file bootstraps them, and how much VM work one unit of execution may do. Read once, at startup.</summary>
public sealed class ScriptEngineOptions
{
    /// <summary>Directory holding init.lua and everything require can reach.</summary>
    public required string ScriptsDirectory { get; init; }

    /// <summary>File executed after the prelude, relative to the scripts directory. Optional on disk.</summary>
    public string BootstrapFile { get; init; } = "init.lua";

    /// <summary>Instructions one resume may execute before it is aborted.</summary>
    public int MaxInstructionsPerResume { get; init; } = 150_000;

    /// <summary>
    /// Instructions one top-level chunk (the prelude, the bootstrap file, a file loaded by <c>LoadFile</c>)
    /// may execute before it is aborted. Larger than the resume budget because a chunk builds content
    /// tables once, while nothing else competes for the loop.
    /// </summary>
    public int MaxInstructionsPerChunk { get; init; } = 10_000_000;

    /// <summary>How often, in instructions, the budget hook runs.</summary>
    public int HookInterval { get; init; } = 1_000;

    /// <summary>Whether .luarc.json and definitions.lua are written at startup.</summary>
    public bool WriteDefinitions { get; init; } = true;

    /// <summary>Checks the options before the engine builds anything, so a misconfiguration is refused at startup rather than at the first script.</summary>
    /// <exception cref="ArgumentException"><see cref="ScriptsDirectory"/> or <see cref="BootstrapFile"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A budget or the hook interval is not positive, or the hook interval exceeds one of the budgets.</exception>
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ScriptsDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(BootstrapFile);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxInstructionsPerResume);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxInstructionsPerChunk);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(HookInterval);

        if (HookInterval > MaxInstructionsPerResume)
        {
            throw new ArgumentOutOfRangeException(nameof(HookInterval), "The hook interval cannot exceed the resume budget.");
        }

        if (HookInterval > MaxInstructionsPerChunk)
        {
            throw new ArgumentOutOfRangeException(nameof(HookInterval), "The hook interval cannot exceed the chunk budget.");
        }
    }
}
