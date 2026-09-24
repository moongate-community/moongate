namespace Moongate.Persistence.Internal;

internal sealed class MigrationDirectoryLock : IDisposable
{
    private readonly List<FileStream> _files;

    private MigrationDirectoryLock(List<FileStream> files)
    {
        _files = files;
    }

    public static async Task<MigrationDirectoryLock> AcquireAsync(
        IEnumerable<string> directories,
        CancellationToken cancellationToken
    )
    {
        List<FileStream> files = [];

        try
        {
            foreach (var directory in directories.Select(Path.GetFullPath)
                                                 .Distinct(StringComparer.Ordinal)
                                                 .Order(StringComparer.Ordinal))
            {
                var path = Path.Combine(directory, ".moongate-generation.lock");

                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        files.Add(new(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));

                        break;
                    }
                    catch (IOException exception) when ((exception.HResult & 0xffff) is 11 or 32 or 33)
                    {
                        await Task.Delay(50, cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            return new(files);
        }
        catch
        {
            foreach (var file in files)
            {
                file.Dispose();
            }

            throw;
        }
    }

    public void Dispose()
    {
        foreach (var file in _files)
        {
            file.Dispose();
        }
    }
}
