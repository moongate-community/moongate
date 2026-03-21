namespace Moongate.UO.Data.Data.Reputation;

/// <summary>
/// Immutable runtime configuration for reputation title formatting.
/// </summary>
public sealed class ReputationTitleConfiguration
{
    /// <summary>
    /// Gets the built-in default configuration.
    /// </summary>
    public static ReputationTitleConfiguration Default { get; } = Create(
        new("Lord", "Lady"),
        [
            new(
                1249,
                [
                    new(-10000, "The Outcast"),
                    new(-5000, "The Despicable"),
                    new(-2500, "The Scoundrel"),
                    new(-1250, "The Unsavory"),
                    new(-625, "The Rude"),
                    new(624, string.Empty),
                    new(1249, "The Fair"),
                    new(2499, "The Kind"),
                    new(4999, "The Good"),
                    new(9999, "The Honest"),
                    new(10000, "The Trustworthy")
                ]
            ),
            new(
                2499,
                [
                    new(-10000, "The Wretched"),
                    new(-5000, "The Dastardly"),
                    new(-2500, "The Malicious"),
                    new(-1250, "The Dishonorable"),
                    new(-625, "The Disreputable"),
                    new(624, "The Notable"),
                    new(1249, "The Upstanding"),
                    new(2499, "The Respectable"),
                    new(4999, "The Honorable"),
                    new(9999, "The Commendable"),
                    new(10000, "The Estimable")
                ]
            ),
            new(
                4999,
                [
                    new(-10000, "The Nefarious"),
                    new(-5000, "The Wicked"),
                    new(-2500, "The Vile"),
                    new(-1250, "The Ignoble"),
                    new(-625, "The Notorious"),
                    new(624, "The Prominent"),
                    new(1249, "The Reputable"),
                    new(2499, "The Proper"),
                    new(4999, "The Admirable"),
                    new(9999, "The Famed"),
                    new(10000, "The Great")
                ]
            ),
            new(
                9999,
                [
                    new(-10000, "The Dread"),
                    new(-5000, "The Evil"),
                    new(-2500, "The Villainous"),
                    new(-1250, "The Sinister"),
                    new(-625, "The Infamous"),
                    new(624, "The Renowned"),
                    new(1249, "The Distinguished"),
                    new(2499, "The Eminent"),
                    new(4999, "The Noble"),
                    new(9999, "The Illustrious"),
                    new(10000, "The Glorious")
                ]
            ),
            new(
                10000,
                [
                    new(-10000, "The Dread"),
                    new(-5000, "The Evil"),
                    new(-2500, "The Dark"),
                    new(-1250, "The Sinister"),
                    new(-625, "The Dishonored"),
                    new(624, string.Empty),
                    new(1249, "The Distinguished"),
                    new(2499, "The Eminent"),
                    new(4999, "The Noble"),
                    new(9999, "The Illustrious"),
                    new(10000, "The Glorious")
                ]
            )
        ]
    );

    /// <summary>
    /// Gets the configured legendary honorific labels.
    /// </summary>
    public required ReputationHonorifics Honorifics { get; init; }

    /// <summary>
    /// Gets the ordered fame buckets used to resolve the title prefix.
    /// </summary>
    public required IReadOnlyList<ReputationFameBucket> FameBuckets { get; init; }

    /// <summary>
    /// Creates an immutable configuration instance.
    /// </summary>
    public static ReputationTitleConfiguration Create(
        ReputationHonorifics honorifics,
        IReadOnlyList<ReputationFameBucket> fameBuckets
    )
    {
        ArgumentNullException.ThrowIfNull(honorifics);
        ArgumentNullException.ThrowIfNull(fameBuckets);

        return new()
        {
            Honorifics = honorifics,
            FameBuckets = fameBuckets
        };
    }
}
