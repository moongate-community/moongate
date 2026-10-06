namespace Moongate.Server.Ultima.Data.Config;

/// <summary>
///     TOML settings for the NPCs.
/// </summary>
public sealed class NpcsConfig
{
    /// <summary>
    ///     Gets or sets the milliseconds between two thinks of an NPC near a player; 500 is ModernUO's passive speed.
    /// </summary>
    public int ThinkIntervalMs { get; set; } = 500;

    /// <summary>
    ///     Gets or sets how near, in cells along X or Y, another mobile must come for an NPC's script to sense it
    ///     ( <c>on_mobile_in_range</c>).
    /// </summary>
    public int SenseRange { get; set; } = 8;

    /// <summary>
    ///     Validates the section before server services begin startup: the interval must be from 50 ms to one minute,
    ///     the sense range from 1 to 24 cells, the farthest a client sees.
    /// </summary>
    public void Validate()
    {
        if (ThinkIntervalMs is < 50 or > 60000)
        {
            throw new InvalidOperationException(
                $"ultima.npcs.think_interval_ms must be from 50 to 60000, found {ThinkIntervalMs}."
            );
        }

        if (SenseRange is < 1 or > 24)
        {
            throw new InvalidOperationException($"ultima.npcs.sense_range must be from 1 to 24, found {SenseRange}.");
        }
    }
}
