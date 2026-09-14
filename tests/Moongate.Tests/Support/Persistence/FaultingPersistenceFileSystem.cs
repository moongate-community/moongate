using Moongate.Persistence.Interfaces.Internal;
using Moongate.Persistence.Internal;

namespace Moongate.Tests.Support.Persistence;

internal sealed class FaultingPersistenceFileSystem : IPersistenceFileSystem, IDisposable
{
    private readonly PhysicalPersistenceFileSystem _physical = new();

    public bool FailJournalOpen { get; set; }

    public bool FailWrite { get; set; }

    public bool FailFlush { get; set; }

    public bool BlockFlush { get; set; }

    public bool FailJournalPublication { get; set; }

    public TaskCompletionSource FlushEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ManualResetEventSlim ContinueFlush { get; } = new();

    public Stream Open(string path, FileMode mode, FileAccess access, FileShare share)
    {
        if (FailJournalOpen && path.EndsWith(".journal.bin", StringComparison.Ordinal))
            throw new IOException("Injected journal open failure.");
        var stream = _physical.Open(path, mode, access, share);
        return path.EndsWith(".journal.bin", StringComparison.Ordinal)
            ? new FaultingPersistenceStream(stream, this)
            : stream;
    }

    public bool Exists(string path) => _physical.Exists(path);

    public void CreateDirectory(string path) => _physical.CreateDirectory(path);

    public void Delete(string path) => _physical.Delete(path);

    public void Move(string source, string destination)
    {
        if (FailJournalPublication && destination.EndsWith(".journal.bin", StringComparison.Ordinal))
            throw new IOException("Injected journal publication failure.");
        _physical.Move(source, destination);
    }

    public void FlushToDisk(Stream stream)
    {
        if (stream is FaultingPersistenceStream faulting)
        {
            if (BlockFlush)
            {
                FlushEntered.TrySetResult();
                if (!ContinueFlush.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Flush was not released.");
            }
            if (FailFlush) throw new IOException("Injected durable flush failure.");
            _physical.FlushToDisk(faulting.Inner);
        }
        else _physical.FlushToDisk(stream);
    }

    public void Dispose()
    {
        ContinueFlush.Dispose();
    }
}
