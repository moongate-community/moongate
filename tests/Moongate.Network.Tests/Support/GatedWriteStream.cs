namespace Moongate.Network.Tests.Support;

public sealed class GatedWriteStream : Stream
{
    private int _disposeCount;
    private int _writeCount;

    public TaskCompletionSource WriteEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource WriteExited { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource ReleaseWrite { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource ReadEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource ReleaseRead { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool HoldReadAfterCancellation { get; set; }
    public Exception? WriteFailure { get; set; }
    public Exception? CancellationFailure { get; set; }
    public Exception? DisposeFailure { get; set; }
    public bool DisposedDuringWrite { get; private set; }
    public int DisposeCount => Volatile.Read(ref _disposeCount);
    public int WriteCount => Volatile.Read(ref _writeCount);
    public override bool CanRead => true;
    public override bool CanWrite => true;
    public override bool CanSeek => false;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() { }

    public override Task FlushAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    public override int Read(byte[] buffer, int offset, int count)
        => throw new NotSupportedException();

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        using var registration = cancellationToken.Register(
            () =>
            {
                if (CancellationFailure is not null)
                {
                    throw CancellationFailure;
                }
            }
        );
        ReadEntered.TrySetResult();

        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        finally
        {
            if (HoldReadAfterCancellation)
            {
                await ReleaseRead.Task;
            }
        }

        return 0;
    }

    public override long Seek(long offset, SeekOrigin origin)
        => throw new NotSupportedException();

    public override void SetLength(long value)
        => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count)
        => throw new NotSupportedException();

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _writeCount);
        WriteEntered.TrySetResult();

        try
        {
            await ReleaseWrite.Task.WaitAsync(cancellationToken);

            if (WriteFailure is not null)
            {
                throw WriteFailure;
            }
        }
        finally
        {
            WriteExited.TrySetResult();
        }
    }

    protected override void Dispose(bool disposing)
    {
        Interlocked.Increment(ref _disposeCount);
        DisposedDuringWrite = WriteEntered.Task.IsCompleted && !WriteExited.Task.IsCompleted;
        base.Dispose(disposing);

        if (DisposeFailure is not null)
        {
            throw DisposeFailure;
        }
    }
}
