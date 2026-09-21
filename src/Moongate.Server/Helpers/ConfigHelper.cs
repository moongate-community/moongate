using Moongate.Core.Utils;
using Moongate.Server.Data.Config;

namespace Moongate.Server.Helpers;

/// <summary>
/// Loads the server configuration or writes the model defaults when the TOML file is missing.
/// </summary>
public static class ConfigHelper
{
    /// <summary>
    /// Reads an existing TOML file or creates it, including any missing parent directories.
    /// </summary>
    /// <remarks>
    /// Property names use snake_case. Existing files are not rewritten, and parsing or I/O errors propagate to the caller.
    /// </remarks>
    public static MoongateServerConfig Load(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (File.Exists(filePath))
        {
            var loaded = TomlUtils.DeserializeFromFile<MoongateServerConfig>(filePath)
                         ?? throw new InvalidDataException(
                             $"Configuration file '{filePath}' did not contain a server configuration."
                         );
            loaded.Validate();
            return loaded;
        }

        var config = new MoongateServerConfig();
        config.Validate();
        TomlUtils.SerializeToFile(config, filePath);

        return config;
    }
}
