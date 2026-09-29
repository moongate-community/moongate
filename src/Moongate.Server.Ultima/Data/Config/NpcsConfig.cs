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
    ///     Validates the section before server services begin startup: the interval must be from 50 ms to one minute.
    /// </summary>
    public void Validate()
    {
        if (ThinkIntervalMs is < 50 or > 60000)
        {
            throw new InvalidOperationException(
                $"ultima.npcs.think_interval_ms must be from 50 to 60000, found {ThinkIntervalMs}."
            );
        }
    }
}
