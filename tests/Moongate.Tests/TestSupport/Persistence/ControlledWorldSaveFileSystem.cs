using System.Reflection;
using Moongate.Persistence.DataAccess;
using Moongate.Persistence.Interfaces.Internal;
using Moongate.Persistence.Internal;
using Moongate.Tests.Support.Persistence;

namespace Moongate.Tests.TestSupport.Persistence;

internal sealed class ControlledWorldSaveFileSystem : IPersistenceFileSystem, IDisposable
{
    private readonly PhysicalPersistenceFileSystem _physical = new();
    private readonly ManualResetEventSlim _release = new();

    public bool BlockFlush { get; set; }
    public Exception? FlushFailure { get; set; }
    public TaskCompletionSource FlushEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Attach(DataAccess<TestEntity> items)
    {
        // Replace only the filesystem boundary; the owner, collections, serialization and durable files stay real.
        var store = typeof(DataAccess<TestEntity>).GetField("_store", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(items)!;
        var files = store.GetType().GetField("_files", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(store)!;
        files.GetType().GetField("_fileSystem", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(files, this);
    }

    public void Release() => _release.Set();
    public Stream Open(string path, FileMode mode, FileAccess access, FileShare share) => _physical.Open(path, mode, access, share);
    public bool Exists(string path) => _physical.Exists(path);
    public void CreateDirectory(string path) => _physical.CreateDirectory(path);
    public void Delete(string path) => _physical.Delete(path);
    public void Move(string source, string destination) => _physical.Move(source, destination);

    public void FlushToDisk(Stream stream)
    {
        if (BlockFlush)
        {
            FlushEntered.TrySetResult();
            if (!_release.Wait(TimeSpan.FromSeconds(10)))
            {
                throw new TimeoutException("The test did not release the durable flush.");
            }
        }
        if (FlushFailure is not null)
        {
            throw FlushFailure;
        }
        _physical.FlushToDisk(stream);
    }

    public void Dispose()
    {
        _release.Dispose();
    }
}
