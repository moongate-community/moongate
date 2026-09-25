using Moongate.Server.Core.Data.Persistence;

namespace Moongate.Server.Data.Config.Sections;

/// <summary>TOML settings for periodic world saves.</summary>
public sealed class WorldSaveConfig
{
    public bool Enabled { get; set; } = true;
    public int IntervalSeconds { get; set; } = 300;

    /// <summary>Maps validated TOML settings to the service's immutable options.</summary>
    public WorldSaveOptions ToOptions()
    {
        Validate();
        var options = new WorldSaveOptions
        {
            Enabled = Enabled,
            Interval = TimeSpan.FromSeconds(IntervalSeconds)
        };
        options.Validate();

        return options;
    }

    /// <summary>Rejects invalid intervals even when automatic saving is disabled.</summary>
    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(IntervalSeconds);
    }
}
