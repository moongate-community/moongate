using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Moongate.Server.Bootstrap.Internal;

/// <summary>Owns the process file and exclusive startup lock for one server root.</summary>
internal sealed class PidFileGuard : IDisposable
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);
    private readonly string _pidFilePath;
    private readonly int _processId;
    private readonly FileStream _instanceLock;
    private bool _disposed;

    private PidFileGuard(string pidFilePath, int processId, FileStream instanceLock)
    {
        _pidFilePath = pidFilePath;
        _processId = processId;
        _instanceLock = instanceLock;
    }

    public static PidFileGuard Acquire(string rootDirectory, string pidNfileName = "moongate.pid")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        var root = Path.GetFullPath(rootDirectory);
        Directory.CreateDirectory(root);
        var pidFilePath = Path.Combine(root, pidNfileName);
        FileStream instanceLock;

        try
        {
            // Keep a separate, stable file: deleting a locked file can allow another
            // process to create and lock a different file at the same path on Unix.
            instanceLock = new FileStream(
                pidFilePath + ".lock",
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None
            );
        }
        catch (IOException exception)
        {
            throw new IOException(
                $"Cannot lock '{pidFilePath}.lock'. Another Moongate instance may already be running or starting. " +
                exception.Message,
                exception
            );
        }

        try
        {
            var existingPid = ReadPid(pidFilePath);

            if (existingPid is { } pid && IsProcessAlive(pid))
            {
                throw new InvalidOperationException(
                    $"Another Moongate instance is already running with PID {pid} for '{root}'."
                );
            }

            var processId = Environment.ProcessId;
            File.WriteAllText(pidFilePath, processId.ToString(CultureInfo.InvariantCulture), Utf8WithoutBom);

            return new PidFileGuard(pidFilePath, processId, instanceLock);
        }
        catch
        {
            instanceLock.Dispose();

            throw;
        }
    }

    private static int? ReadPid(string path)
    {
        try
        {
            var content = File.ReadAllText(path, Encoding.UTF8).Trim().TrimStart('\uFEFF');

            return int.TryParse(content, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid) && pid > 0
                       ? pid
                       : null;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    private static bool IsProcessAlive(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);

            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            if (ReadPid(_pidFilePath) == _processId)
            {
                File.Delete(_pidFilePath);
            }
        }
        finally
        {
            // The lock file remains on disk; only the open handle denotes ownership.
            _instanceLock.Dispose();
        }
    }
}
