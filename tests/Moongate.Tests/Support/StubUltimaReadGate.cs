using Moongate.Http.Plugin.Interfaces.Ultima;

namespace Moongate.Tests.Support;

/// <summary>Runs the read inline: fakes have no statics to guard.</summary>
public sealed class StubUltimaReadGate : IUltimaReadGate
{
    public Task<T> ReadAsync<T>(Func<T> read, CancellationToken cancellationToken = default)
        => Task.FromResult(read());
}
