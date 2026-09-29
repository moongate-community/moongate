using Moongate.Core.Utils;
using Moongate.Server.Data.Config;
using Tomlyn;
using Tomlyn.Model;

namespace Moongate.Server.Helpers;

/// <summary>
///     Loads the server configuration or writes the model defaults when the TOML file is missing.
/// </summary>
public static class ConfigHelper
{
    // Top-level sections that moved under [ultima]; a file that still has one would otherwise lose its values silently.
    private static readonly string[] MovedUnderUltima =
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
            RejectMovedSettings(filePath);

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

    private static void RejectMovedSettings(string filePath)
    {
        var document = TomlSerializer.Deserialize<TomlTable>(File.ReadAllText(filePath));

        if (document is null)
        {
            return;
        }

        foreach (var section in MovedUnderUltima)
        {
            if (document.ContainsKey(section))
            {
                throw new InvalidOperationException(
                    $"'{filePath}': the [{section}] section moved to [ultima.{section}]; move its keys there."
                );
            }
        }

        if (document.TryGetValue("ultima", out var ultima) &&
            ultima is TomlTable ultimaTable &&
            ultimaTable.TryGetValue("starting_items", out var startingItems) &&
            startingItems is TomlTable startingItemsTable &&
            startingItemsTable.ContainsKey("gold"))
        {
            throw new InvalidOperationException(
                $"'{filePath}': starting gold is no longer a setting; give it with a common set in " +
                "data/starting_items.toml and remove ultima.starting_items.gold."
            );
        }
    }
}
