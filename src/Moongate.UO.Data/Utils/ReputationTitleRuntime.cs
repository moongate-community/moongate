using Moongate.UO.Data.Data.Reputation;

namespace Moongate.UO.Data.Utils;

/// <summary>
/// Holds the active startup-loaded reputation title configuration.
/// </summary>
public static class ReputationTitleRuntime
{
    /// <summary>
    /// Gets the current active reputation title configuration.
    /// </summary>
    public static ReputationTitleConfiguration Current { get; private set; } = ReputationTitleConfiguration.Default;

    /// <summary>
    /// Replaces the active configuration.
    /// </summary>
    public static void Configure(ReputationTitleConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        Current = configuration;
    }

    /// <summary>
    /// Restores the built-in default configuration.
    /// </summary>
    public static void Reset()
        => Current = ReputationTitleConfiguration.Default;
}
