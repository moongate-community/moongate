using Moongate.Persistence.Types.Persistence;

namespace Moongate.Persistence.Interfaces;

/// <summary>
///     Exports the data of the configured databases as SQL scripts.
/// </summary>
public interface IPersistenceDataExporter
{
    /// <summary>
    ///     Gets the database targets this process is configured for.
    /// </summary>
    IReadOnlyCollection<PersistenceDatabaseTarget> ConfiguredTargets { get; }

    /// <summary>
    ///     Writes every row of one target as a data-only script of <c>COPY ... FROM stdin</c> blocks.
    /// </summary>
    /// <remarks>
    ///     The script is one consistent picture of the database. It restores onto a database that already has the
    ///     schema. The stream is left open.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    ///     Persistence is not initialized, the target is not configured, or two tables reference each other.
    /// </exception>
    Task ExportDataAsync(PersistenceDatabaseTarget target, Stream output, CancellationToken cancellationToken = default);
}
