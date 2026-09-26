namespace Moongate.Server.Core.Data.Config;

/// <summary>
///     TOML settings for line of sight checks.
/// </summary>
public sealed class LineOfSightConfig
{
    /// <summary>
    ///     Gets or sets the farthest a point can see, in cells along X or Y; farther points are never in sight.
    /// </summary>
    public int MaxDistance { get; set; } = 25;

    /// <summary>
    ///     Validates the section before server services begin startup: the distance must be from 1 to 255.
    /// </summary>
    public void Validate()
    {
        if (MaxDistance is < 1 or > 255)
        {
            throw new InvalidOperationException(
                $"line_of_sight.max_distance must be from 1 to 255, found {MaxDistance}."
            );
        }
    }
}
