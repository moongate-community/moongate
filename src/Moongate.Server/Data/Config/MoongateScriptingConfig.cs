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
}
