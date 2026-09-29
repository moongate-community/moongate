using Moongate.Server.Core.Data.Config;

namespace Moongate.Tests.TestSupport.Config;

/// <summary>
///     Config documents for containers that run a plugin's <c>Register</c> directly, as the host registers one before it.
/// </summary>
public static class TestConfigDocuments
{
    /// <summary>
    ///     An empty <c>moongate.toml</c> in <paramref name="directory" />: every plugin section gets its defaults, which
    ///     are appended to that file.
    /// </summary>
    public static ServerConfigDocument Empty(string directory)
    {
        return new(Path.Combine(directory, "moongate.toml"), new(), []);
    }
}
