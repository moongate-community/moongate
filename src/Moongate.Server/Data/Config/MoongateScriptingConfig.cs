namespace Moongate.Server.Data.Config;

/// <summary>
/// Scripting runtime configuration.
/// </summary>
public sealed class MoongateScriptingConfig
{
    /// <summary>
    /// Enables file-system watcher for Lua script live reload.
    /// </summary>
    public bool EnableFileWatcher { get; set; } = true;

    /// <summary>
    /// Maximum number of due Lua brains processed per tick.
    /// Value <= 0 means no explicit limit.
    /// </summary>
    public int LuaBrainMaxBrainsPerTick { get; set; }
}
