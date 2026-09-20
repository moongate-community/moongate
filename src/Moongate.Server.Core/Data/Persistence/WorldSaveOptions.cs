namespace Moongate.Server.Core.Data.Persistence;

/// <summary>Controls automatic world saves.</summary>
public sealed class WorldSaveOptions
{
    public bool Enabled { get; init; } = true;
    public TimeSpan Interval { get; init; } = TimeSpan.FromSeconds(300);

    /// <summary>Rejects invalid configuration before scheduling or persistence work begins.</summary>
    public void Validate()
    {
        if (Interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(Interval), "The world-save interval must be positive.");
        }
    }
}
