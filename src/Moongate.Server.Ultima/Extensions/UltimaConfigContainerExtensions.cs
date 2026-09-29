using DryIoc;
using Moongate.Server.Core.Data.Config;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Ultima.Data.Config;
using Tomlyn.Model;

namespace Moongate.Server.Ultima.Extensions;

/// <summary>
///     Registers the <c>[ultima]</c> section of <c>config/moongate.toml</c> and each of its sub-tables.
/// </summary>
public static class UltimaConfigContainerExtensions
{
    // Top-level sections that moved under [ultima]; a file that still has one would otherwise lose its values silently.
    private static readonly string[] MovedUnderUltima =
        ["localization", "line_of_sight", "world", "items", "starting_items", "characters"];

    /// <summary>
    ///     Adds <see cref="UltimaConfig" /> with <c>AddConfig</c> and registers its sub-sections, so services keep
    ///     receiving <see cref="WorldConfig" />, <see cref="CharactersConfig" /> and the others.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     The file still has a section in its old place, or a value is not valid.
    /// </exception>
    public static UltimaConfig AddUltimaConfig(this Container container)
    {
        ArgumentNullException.ThrowIfNull(container);

        var document = container.Resolve<ServerConfigDocument>();
        RejectMovedSettings(document);

        var ultima = container.AddConfig<UltimaConfig>("ultima");
        container.RegisterInstance(ultima.Localization);
        container.RegisterInstance(ultima.LineOfSight);
        container.RegisterInstance(ultima.World);
        container.RegisterInstance(ultima.Items);
        container.RegisterInstance(ultima.StartingItems);
        container.RegisterInstance(ultima.Characters);

        return ultima;
    }

    private static void RejectMovedSettings(ServerConfigDocument document)
    {
        foreach (var section in MovedUnderUltima)
        {
            if (document.Table.ContainsKey(section))
            {
                throw new InvalidOperationException(
                    $"'{document.FilePath}': the [{section}] section moved to [ultima.{section}]; move its keys there."
                );
            }
        }

        if (document.Table.TryGetValue("ultima", out var ultima) &&
            ultima is TomlTable ultimaTable &&
            ultimaTable.TryGetValue("starting_items", out var startingItems) &&
            startingItems is TomlTable startingItemsTable &&
            startingItemsTable.ContainsKey("gold"))
        {
            throw new InvalidOperationException(
                $"'{document.FilePath}': starting gold is no longer a setting; give it with a common set in " +
                "data/starting_items.toml and remove ultima.starting_items.gold."
            );
        }
    }
}
