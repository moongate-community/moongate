namespace Moongate.Tests.TestSupport.Ctl;

/// <summary>
///     A fact that is skipped unless the named tool, such as bash, is on the PATH.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class ToolFactAttribute : FactAttribute
{
    public ToolFactAttribute(string tool)
    {
        var directories = (System.Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator);

        if (!directories.Any(directory => File.Exists(Path.Combine(directory, tool))))
        {
            Skip = $"{tool} is not available";
        }
    }
}
