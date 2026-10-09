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

    public NpcsConfig Npcs { get; set; } = new();

    public RegenerationConfig Regeneration { get; set; } = new();

    public CrimeConfig Crime { get; set; } = new();

    public MurderConfig Murder { get; set; } = new();

    public SkillsConfig Skills { get; set; } = new();

    public CombatConfig Combat { get; set; } = new();

    public SpawnsConfig Spawns { get; set; } = new();

    public JailConfig Jail { get; set; } = new();

    public HelpConfig Help { get; set; } = new();

    public ScheduleConfig Schedule { get; set; } = new();

    public BulletinBoardsConfig BulletinBoards { get; set; } = new();

    public BankConfig Bank { get; set; } = new();

    public StableConfig Stable { get; set; } = new();

    /// <summary>
    ///     Validates the sub-tables before server services begin startup.
    /// </summary>
    public void Validate()
    {
        if (Spawns is null)
        {
            throw new InvalidOperationException("The ultima.spawns configuration section cannot be null.");
        }

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

        if (Npcs is null)
        {
            throw new InvalidOperationException("The ultima.npcs configuration section cannot be null.");
        }

        Npcs.Validate();

        if (Regeneration is null)
        {
            throw new InvalidOperationException("The ultima.regeneration configuration section cannot be null.");
        }

        Regeneration.Validate();

        if (Crime is null)
        {
            throw new InvalidOperationException("The ultima.crime configuration section cannot be null.");
        }

        Crime.Validate();

        if (Murder is null)
        {
            throw new InvalidOperationException("The ultima.murder configuration section cannot be null.");
        }

        Murder.Validate();

        if (Skills is null)
        {
            throw new InvalidOperationException("The ultima.skills configuration section cannot be null.");
        }

        Skills.Validate();

        if (Combat is null)
        {
            throw new InvalidOperationException("The ultima.combat configuration section cannot be null.");
        }

        Combat.Validate();

        if (Jail is null)
        {
            throw new InvalidOperationException("The ultima.jail configuration section cannot be null.");
        }

        Jail.Validate();

        if (Help is null)
        {
            throw new InvalidOperationException("The ultima.help configuration section cannot be null.");
        }

        Help.Validate();

        if (Schedule is null)
        {
            throw new InvalidOperationException("The ultima.schedule configuration section cannot be null.");
        }

        Schedule.Validate();

        if (BulletinBoards is null)
        {
            throw new InvalidOperationException("The ultima.bulletin_boards configuration section cannot be null.");
        }

        BulletinBoards.Validate();

        if (Bank is null)
        {
            throw new InvalidOperationException("The ultima.bank configuration section cannot be null.");
        }

        Bank.Validate();

        if (Stable is null)
        {
            throw new InvalidOperationException("The ultima.stable configuration section cannot be null.");
        }

        Stable.Validate();
    }
}
