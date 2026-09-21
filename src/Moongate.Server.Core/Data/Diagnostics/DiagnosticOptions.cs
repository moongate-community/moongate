namespace Moongate.Server.Core.Data.Diagnostics;

public sealed class DiagnosticOptions
{
    private static readonly TimeSpan MinimumInterval = TimeSpan.FromMilliseconds(1);
    private static readonly TimeSpan MaximumInterval = TimeSpan.FromMilliseconds(4_294_967_294d);

    public bool Enabled { get; init; } = true;
    public TimeSpan Interval { get; init; } = TimeSpan.FromSeconds(5);
    public bool LogMetrics { get; init; } = false;

    public void Validate()
    {
        if (Interval < MinimumInterval || Interval > MaximumInterval)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Interval),
                $"The diagnostic interval must be between {MinimumInterval.TotalMilliseconds} and " +
                $"{MaximumInterval.TotalMilliseconds} milliseconds."
            );
        }
    }
}
