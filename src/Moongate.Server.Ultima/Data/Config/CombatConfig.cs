namespace Moongate.Server.Ultima.Data.Config;

/// <summary>
///     TOML settings for melee combat: how fast the fighters swing, what a swing costs and how far it reaches.
/// </summary>
public sealed class CombatConfig
{
    private const double MaximumFactor = 100;
    private const int MaximumStamina = 100;
    private const int MaximumRange = 24;
    private const int MaximumSeconds = 3600;

    /// <summary>
    ///     Gets or sets the factor that divides the delay between two swings: 2 makes every fighter swing twice as
    ///     often, 0.5 half as often. 1 is the classic speed.
    /// </summary>
    public double GlobalAttackSpeed { get; set; } = 1.0;

    /// <summary>
    ///     Gets or sets the stamina a swing costs a player; 0 costs nothing, as ModernUO. UOX3 takes 2.
    /// </summary>
    public int AttackStamina { get; set; }

    /// <summary>
    ///     Gets or sets the number that divides the damage an NPC does to a player: 2 halves it, 1 leaves it.
    /// </summary>
    public double NpcDamageRate { get; set; } = 1.0;

    /// <summary>
    ///     Gets or sets how many tiles a melee swing reaches; 1 is the next tile.
    /// </summary>
    public int MaxRange { get; set; } = 1;

    /// <summary>
    ///     Gets or sets the seconds a fighter keeps its target without swinging or being hit, as ModernUO's minute.
    /// </summary>
    public int CombatantSeconds { get; set; } = 60;

    /// <summary>
    ///     Gets or sets whether the damage of a swing shows over the head of the one hit, to the players in the fight.
    /// </summary>
    public bool DisplayDamageNumbers { get; set; } = true;

    /// <summary>
    ///     Validates the section before server services begin startup.
    /// </summary>
    public void Validate()
    {
        if (double.IsNaN(GlobalAttackSpeed) || GlobalAttackSpeed is <= 0 or > MaximumFactor)
        {
            throw new InvalidOperationException(
                $"ultima.combat.global_attack_speed must be above 0 and at most {MaximumFactor}, found {GlobalAttackSpeed}."
            );
        }

        if (AttackStamina is < 0 or > MaximumStamina)
        {
            throw new InvalidOperationException(
                $"ultima.combat.attack_stamina must be from 0 to {MaximumStamina}, found {AttackStamina}."
            );
        }

        if (double.IsNaN(NpcDamageRate) || NpcDamageRate is <= 0 or > MaximumFactor)
        {
            throw new InvalidOperationException(
                $"ultima.combat.npc_damage_rate must be above 0 and at most {MaximumFactor}, found {NpcDamageRate}."
            );
        }

        if (MaxRange is < 1 or > MaximumRange)
        {
            throw new InvalidOperationException($"ultima.combat.max_range must be from 1 to {MaximumRange}, found {MaxRange}.");
        }

        if (CombatantSeconds is < 1 or > MaximumSeconds)
        {
            throw new InvalidOperationException(
                $"ultima.combat.combatant_seconds must be from 1 to {MaximumSeconds}, found {CombatantSeconds}."
            );
        }
    }
}
