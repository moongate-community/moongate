using Moongate.Server.Core.Data.Config;
using Tomlyn;
using Tomlyn.Model;

namespace Moongate.Tests.TestSupport.Config;

/// <summary>
///     Config documents for containers that run a plugin's <c>Register</c> directly, as the host registers one before
///     it.
/// </summary>
public static class TestConfigDocuments
{
    /// <summary>
    ///     An empty <c>moongate.toml</c> in <paramref name="directory" />: every plugin section gets its defaults,
    ///     which
    ///     are appended to that file.
    /// </summary>
    public static ServerConfigDocument Empty(string directory)
    {
        return new(Path.Combine(directory, "moongate.toml"), new(), []);
    }

    /// <summary>
    ///     A <c>moongate.toml</c> in <paramref name="directory" /> holding <paramref name="toml" />.
    /// </summary>
    public static ServerConfigDocument FromToml(string directory, string toml)
    {
        var path = Path.Combine(directory, "moongate.toml");
        File.WriteAllText(path, toml);

        return new(path, TomlSerializer.Deserialize<TomlTable>(toml)!, []);
    }
}
