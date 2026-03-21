using Moongate.UO.Data.Data.Reputation;

namespace Moongate.UO.Data.Utils;

/// <summary>
/// Holds the active startup-loaded reputation title configuration.
/// </summary>
public static class ReputationTitleRuntime
{
    private static ReputationTitleConfiguration _current = ReputationTitleConfiguration.Default;

    /// <summary>
    /// Gets the current active reputation title configuration.
    /// </summary>
    public static ReputationTitleConfiguration Current => _current;

    /// <summary>
    /// Replaces the active configuration.
    /// </summary>
    public static void Configure(ReputationTitleConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _current = configuration;
    }

    /// <summary>
    /// Restores the built-in default configuration.
    /// </summary>
    public static void Reset()
    {
        _current = ReputationTitleConfiguration.Default;
    }
}
