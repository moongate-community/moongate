using System.Text.Json;
using Moongate.Core.Utils;
using Moongate.Server.Core.Data.Config;
using Moongate.Server.Data.Config;
using Tomlyn;
using Tomlyn.Model;

namespace Moongate.Server.Helpers;

/// <summary>
///     Loads the server configuration or writes the model defaults when the TOML file is missing.
/// </summary>
public static class ConfigHelper
{
    // Old top-level names of the [ultima] sub-tables: the Ultima plugin refuses them, so no plugin may take them.
    private static readonly string[] RetiredSections =
        ["localization", "line_of_sight", "world", "items", "starting_items", "characters"];

    /// <summary>
    ///     Reads an existing TOML file or creates it, including any missing parent directories.
    /// </summary>
    /// <remarks>
    ///     Property names use snake_case. Existing files are not rewritten, and parsing or I/O errors propagate to the caller.
    /// </remarks>
    public static MoongateServerConfig Load(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (File.Exists(filePath))
        {
            var loaded = TomlUtils.DeserializeFromFile<MoongateServerConfig>(filePath) ??
                         throw new InvalidDataException(
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

    /// <summary>
    ///     Parses the configuration file for the plugins, reserving the sections of <see cref="MoongateServerConfig" />
    ///     and the old top-level names of the <c>[ultima]</c> sub-tables.
    /// </summary>
    /// <remarks>
    ///     Call it after <see cref="Load" />, which creates a missing file.
    /// </remarks>
    public static ServerConfigDocument ReadDocument(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var table = TomlSerializer.Deserialize<TomlTable>(File.ReadAllText(filePath)) ?? new TomlTable();
        var sections = typeof(MoongateServerConfig).GetProperties()
                                                   .Select(property => JsonNamingPolicy.SnakeCaseLower.ConvertName(property.Name))
                                                   .Concat(RetiredSections);

        return new(filePath, table, sections);
    }
}
