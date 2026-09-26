using Moongate.Persistence.Migrations.Data.Migrations;

namespace Moongate.Persistence.Migrations.Services;

/// <summary>
///     Prevents execution of generated SQL that still needs a developer review.
/// </summary>
public static class MigrationReviewGuard
{
    public const string Marker = "-- moongate:review-required";

    public static void Validate(IEnumerable<MigrationScript> scripts)
    {
        foreach (var script in scripts)
        {
            if (script.Sql.Contains(Marker, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Migration '{script.Name}' requires review. Review the SQL and remove '{Marker}' before applying it."
                );
            }
        }
    }
}
