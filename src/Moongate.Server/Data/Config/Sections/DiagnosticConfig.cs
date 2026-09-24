using Moongate.Server.Core.Data.Diagnostics;

namespace Moongate.Server.Data.Config.Sections;

/// <summary>TOML settings for periodic diagnostic snapshots.</summary>
public sealed class DiagnosticConfig
{
    public bool Enabled { get; set; } = true;
    public int IntervalSeconds { get; set; } = 5;
    public bool LogMetrics { get; set; }

    /// <summary>Maps validated TOML settings to immutable service options.</summary>
    public DiagnosticOptions ToOptions()
    {
        var options = new DiagnosticOptions
        {
            Enabled = Enabled,
            Interval = TimeSpan.FromSeconds(IntervalSeconds),
            LogMetrics = LogMetrics
        };
        options.Validate();

        return options;
    }
}
