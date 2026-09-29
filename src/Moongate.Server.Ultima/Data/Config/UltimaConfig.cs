using Moongate.Server.Core.Interfaces.Config;

namespace Moongate.Server.Ultima.Data.Config;

/// <summary>
///     The <c>[ultima]</c> section: the client files and, as sub-tables, the gameplay settings of the game server.
/// </summary>
public class UltimaConfig : IConfigSection
{
    public string UltimaPath { get; set; } = "ChangeMe";

    public LocalizationConfig Localization { get; set; } = new();

    public LineOfSightConfig LineOfSight { get; set; } = new();

    public WorldConfig World { get; set; } = new();

    public ItemsConfig Items { get; set; } = new();

    public StartingItemsConfig StartingItems { get; set; } = new();

    public CharactersConfig Characters { get; set; } = new();

    /// <summary>
    ///     Validates the sub-tables before server services begin startup.
    /// </summary>
    public void Validate()
    {
        if (Localization is null)
        {
            throw new InvalidOperationException("The ultima.localization configuration section cannot be null.");
        }

        Localization.Validate();

        if (LineOfSight is null)
        {
            throw new InvalidOperationException("The ultima.line_of_sight configuration section cannot be null.");
        }

        LineOfSight.Validate();

        if (World is null)
        {
            throw new InvalidOperationException("The ultima.world configuration section cannot be null.");
        }

        World.Validate();

        if (Items is null)
        {
            throw new InvalidOperationException("The ultima.items configuration section cannot be null.");
        }

        Items.Validate();

        if (StartingItems is null)
        {
            throw new InvalidOperationException("The ultima.starting_items configuration section cannot be null.");
        }

        StartingItems.Validate();

        if (Characters is null)
        {
            throw new InvalidOperationException("The ultima.characters configuration section cannot be null.");
        }

        Characters.Validate();
    }
}
