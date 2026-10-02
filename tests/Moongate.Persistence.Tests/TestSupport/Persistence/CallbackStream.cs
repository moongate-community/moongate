namespace Moongate.Persistence.Tests.TestSupport.Persistence;

/// <summary>
///     A memory stream that runs a callback once, at the first write.
/// </summary>
public sealed class CallbackStream : MemoryStream
{
    private readonly Action _onFirstWrite;
    private bool _called;

    public CallbackStream(Action onFirstWrite)
    {
        _onFirstWrite = onFirstWrite;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        Notify();
        base.Write(buffer, offset, count);
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Notify();

        return base.WriteAsync(buffer, cancellationToken);
    }

    private void Notify()
    {
        if (_called)
        {
            return;
        }

        _called = true;
        _onFirstWrite();
    }
}
