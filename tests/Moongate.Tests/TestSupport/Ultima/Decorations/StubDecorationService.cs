using Moongate.Server.Ultima.Data.Decorations;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Tests.TestSupport.Ultima.Decorations;

/// <summary>
///     Reports the file results it was given, then returns their totals, or throws <see cref="Failure" /> when set.
/// </summary>
public sealed class StubDecorationService : IDecorationService
{
    private readonly DecorationFileResult[] _files;

    public Exception? Failure { get; init; }

    public int Calls { get; private set; }

    public bool IsRunning { get; set; }

    public StubDecorationService(params DecorationFileResult[] files)
    {
        _files = files;
    }

    public Task<DecorationResult> DecorateAsync(
        IProgress<DecorationFileResult>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        Calls++;

        if (Failure is not null)
        {
            throw Failure;
        }

        foreach (var file in _files)
        {
            progress?.Report(file);
        }

        return Task.FromResult(
            new DecorationResult(
                _files.Sum(file => file.Placed),
                _files.Sum(file => file.Present),
                _files.Sum(file => file.Skipped),
                _files.Length
            )
        );
    }
}
