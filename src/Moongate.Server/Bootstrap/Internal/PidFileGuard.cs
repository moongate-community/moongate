using System.Diagnostics;
using System.Text;

namespace Moongate.Server.Bootstrap.Internal;

internal sealed class PidFileGuard : IDisposable
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);
    private readonly string _pidFilePath;
    private readonly int _currentProcessId;
    private bool _disposed;

    private PidFileGuard(string pidFilePath, int currentProcessId)
    {
        _pidFilePath = pidFilePath;
        _currentProcessId = currentProcessId;
    }

    public static PidFileGuard Acquire(string rootDirectory)
        => Acquire(
            rootDirectory,
            () => Environment.ProcessId,
            static pid =>
            {
                try
                {
                    _ = Process.GetProcessById(pid);

                    return true;
                }
                catch (ArgumentException)
                {
                    return false;
                }
            }
        );

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (!File.Exists(_pidFilePath))
        {
            return;
        }

        var existingRaw = ReadPidFile(_pidFilePath);

        if (!int.TryParse(existingRaw, out var existingPid) || existingPid != _currentProcessId)
        {
            return;
        }

        File.Delete(_pidFilePath);
    }

    internal static PidFileGuard Acquire(
        string rootDirectory,
        Func<int> currentProcessIdProvider,
        Func<int, bool> processExists
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ArgumentNullException.ThrowIfNull(currentProcessIdProvider);
        ArgumentNullException.ThrowIfNull(processExists);

        Directory.CreateDirectory(rootDirectory);

        var pidFilePath = Path.Combine(rootDirectory, "moongate.pid");
        var currentProcessId = currentProcessIdProvider();

        if (File.Exists(pidFilePath))
        {
            var existingRaw = ReadPidFile(pidFilePath);

            if (int.TryParse(existingRaw, out var existingPid) &&
                existingPid > 0 &&
                existingPid != currentProcessId &&
                processExists(existingPid))
            {
                throw new InvalidOperationException(
                    $"Another Moongate instance is already running with PID {existingPid}. " +
                    $"Remove '{pidFilePath}' only if that process is no longer alive."
                );
            }
        }

        File.WriteAllText(pidFilePath, currentProcessId.ToString(), Utf8WithoutBom);

        return new(pidFilePath, currentProcessId);
    }

    private static string ReadPidFile(string pidFilePath)
        => File.ReadAllText(pidFilePath, Encoding.UTF8)
               .Trim()
               .TrimStart('\uFEFF');
}
