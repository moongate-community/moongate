namespace Moongate.Server.Data.Startup;

/// <summary>
/// Represents the fully composed startup loadout for a new character.
/// </summary>
public sealed class StartupLoadout
{
    /// <summary>
    /// Gets startup backpack items.
    /// </summary>
    public List<StartupLoadoutItem> Backpack { get; } = [];

    /// <summary>
    /// Gets startup equipped items.
    /// </summary>
    public List<StartupLoadoutItem> Equip { get; } = [];
}
