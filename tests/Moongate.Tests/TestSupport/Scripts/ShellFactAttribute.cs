namespace Moongate.Tests.TestSupport.Scripts;

/// <summary>
///     A fact that is skipped unless the shell and the tools scripts/install.sh needs are available.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class ShellFactAttribute : FactAttribute
{
    /// <summary>
    ///     Skips the fact when the shell or one of the tools the script needs is missing.
    /// </summary>
    public ShellFactAttribute()
    {
        if (!ScriptedInstall.AreToolsAvailable())
        {
            Skip = "sh, tar, curl or sha256sum is not available";
        }
    }
}
