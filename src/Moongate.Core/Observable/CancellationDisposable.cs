namespace Moongate.Core.Observable;

public class CancellationDisposable(CancellationTokenSource cts) : IDisposable
{
    public void Dispose() => cts.Cancel();
}
