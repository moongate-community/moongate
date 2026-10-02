using System.Text;
using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Types.Persistence;

namespace Moongate.Tests.TestSupport.Persistence;

/// <summary>
///     Writes a fixed text for each target and records the order of the calls.
/// </summary>
public sealed class RecordingDataExporter : IPersistenceDataExporter
{
    public List<PersistenceDatabaseTarget> Calls { get; } = [];

    public HashSet<PersistenceDatabaseTarget> Failing { get; } = [];

    /// <summary>
    ///     When set, every export waits on this before it writes.
    /// </summary>
    public TaskCompletionSource? Gate { get; set; }

    public IReadOnlyCollection<PersistenceDatabaseTarget> ConfiguredTargets { get; }

    public RecordingDataExporter(params PersistenceDatabaseTarget[] targets)
    {
        ConfiguredTargets = targets;
    }

    public async Task ExportDataAsync(
        PersistenceDatabaseTarget target,
        Stream output,
        CancellationToken cancellationToken = default
    )
    {
        Calls.Add(target);

        if (Gate is not null)
        {
            await Gate.Task.WaitAsync(cancellationToken);
        }

        await output.WriteAsync(Encoding.UTF8.GetBytes($"-- {target}\n"), cancellationToken);

        if (Failing.Contains(target))
        {
            throw new InvalidOperationException($"export of {target} broke");
        }
    }
}
