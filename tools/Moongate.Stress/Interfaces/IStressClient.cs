namespace Moongate.Stress.Interfaces;

public interface IStressClient : IAsyncDisposable
{
    int ClientIndex { get; }

    Task RunAsync(CancellationToken cancellationToken = default);
}
