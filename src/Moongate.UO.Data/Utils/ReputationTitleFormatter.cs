using Moongate.UO.Data.Data.Reputation;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Utils;

/// <summary>
/// Formats fame and karma reputation titles for display surfaces such as the paperdoll.
/// </summary>
public static class ReputationTitleFormatter
{
    /// <summary>
    /// Builds the paperdoll-ready display name using fame, karma, honorifics, and the custom title suffix.
    /// </summary>
    /// <param name="mobile">The mobile to format.</param>
    /// <returns>The formatted display name.</returns>
    public static string FormatDisplayName(UOMobileEntity mobile)
    {
        ArgumentNullException.ThrowIfNull(mobile);

        var configuration = ReputationTitleRuntime.Current;
        var parts = new List<string>(4);
        var reputationTitle = GetReputationTitle(mobile.Fame, mobile.Karma, configuration);

        if (!string.IsNullOrWhiteSpace(reputationTitle))
        {
            parts.Add(reputationTitle);
        }

        if (mobile.Fame >= 10000)
        {
            parts.Add(mobile.Gender == GenderType.Female ? configuration.Honorifics.Female : configuration.Honorifics.Male);
        }

        if (!string.IsNullOrWhiteSpace(mobile.Name))
        {
            parts.Add(mobile.Name);
        }

        if (!string.IsNullOrWhiteSpace(mobile.Title))
        {
            parts.Add(mobile.Title);
        }

        return string.Join(' ', parts);
    }

    private static string GetReputationTitle(int fame, int karma, ReputationTitleConfiguration configuration)
    {
        var fameEntry = configuration.FameBuckets[^1];

        foreach (var candidate in configuration.FameBuckets)
        {
            fameEntry = candidate;

            if (fame <= candidate.MaxFame)
            {
                break;
            }
        }

        var karmaEntry = fameEntry.KarmaBuckets[^1];

        foreach (var candidate in fameEntry.KarmaBuckets)
        {
            karmaEntry = candidate;

            if (karma <= candidate.MaxKarma)
            {
                break;
            }
        }

        return karmaEntry.Title;
    }
}
