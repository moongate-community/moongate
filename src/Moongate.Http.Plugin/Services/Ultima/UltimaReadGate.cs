using Moongate.Http.Plugin.Interfaces.Ultima;

namespace Moongate.Http.Plugin.Services.Ultima;

/// <summary>
/// The process-wide gate over Moongate.Ultima's statics. Registered as a singleton, which is the only
/// registration that makes sense: a second instance is a second gate, and a second gate is no gate.
/// </summary>
public sealed class UltimaReadGate : IUltimaReadGate, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<T> ReadAsync<T>(Func<T> read, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);

        try
        {
            return read();
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
        => _gate.Dispose();
}
