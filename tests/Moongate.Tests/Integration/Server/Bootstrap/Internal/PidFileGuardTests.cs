using System.Diagnostics;
using System.Globalization;
using System.Text;
using Moongate.Server.Bootstrap.Internal;
using Moongate.Tests.TestSupport.Directories;

namespace Moongate.Tests.Integration.Server.Bootstrap.Internal;

public sealed class PidFileGuardTests
{
    [Fact]
    public void Acquire_MissingRoot_CreatesPidAndRemovesItOnDispose()
    {
        using var directory = new TemporaryDirectory();
        var root = Path.Combine(directory.Path, "server");
        var pidPath = Path.Combine(root, "moongate.pid");

        using (PidFileGuard.Acquire(root))
        {
            Assert.Equal(Environment.ProcessId.ToString(CultureInfo.InvariantCulture), File.ReadAllText(pidPath));
        }

        Assert.False(File.Exists(pidPath));
        using var next = PidFileGuard.Acquire(root);
    }

    [Fact]
    public async Task Acquire_ExitedProcess_ReplacesStalePid()
    {
        using var directory = new TemporaryDirectory();
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            ArgumentList = { "--version" },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        })!;
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
        var pidPath = directory.CreateFile("moongate.pid", process.Id.ToString(CultureInfo.InvariantCulture));

        using var guard = PidFileGuard.Acquire(directory.Path);

        Assert.Equal(Environment.ProcessId.ToString(CultureInfo.InvariantCulture), File.ReadAllText(pidPath));
    }

    [Theory, InlineData(""), InlineData("invalid"), InlineData("0"), InlineData("-1"), InlineData("999999999999999999999")]
    public void Acquire_InvalidPid_ReplacesFile(string content)
    {
        using var directory = new TemporaryDirectory();
        var pidPath = directory.CreateFile("moongate.pid", content);

        using var guard = PidFileGuard.Acquire(directory.Path);

        Assert.Equal(Environment.ProcessId.ToString(CultureInfo.InvariantCulture), File.ReadAllText(pidPath));
    }

    [Theory, InlineData(false), InlineData(true)]
    public void Acquire_LiveProcess_RejectsWithoutChangingPid(bool withBom)
    {
        using var directory = new TemporaryDirectory();
        var pidPath = Path.Combine(directory.Path, "moongate.pid");
        var content = Environment.ProcessId.ToString(CultureInfo.InvariantCulture);
        File.WriteAllText(pidPath, content, new UTF8Encoding(withBom));
        var original = File.ReadAllBytes(pidPath);

        var exception = Assert.Throws<InvalidOperationException>(() => PidFileGuard.Acquire(directory.Path));

        Assert.Contains(content, exception.Message, StringComparison.Ordinal);
        Assert.Equal(original, File.ReadAllBytes(pidPath));

        File.Delete(pidPath);
        using var next = PidFileGuard.Acquire(directory.Path);
    }

    [Fact]
    public void Acquire_HeldLockAndMissingPid_StillRejectsSecondOwner()
    {
        using var directory = new TemporaryDirectory();
        using var guard = PidFileGuard.Acquire(directory.Path);
        File.Delete(Path.Combine(directory.Path, "moongate.pid"));

        Assert.Throws<IOException>(() => PidFileGuard.Acquire(directory.Path));
    }

    [Fact]
    public void Acquire_DifferentRoots_AllowsIndependentInstances()
    {
        using var first = new TemporaryDirectory();
        using var second = new TemporaryDirectory();
        using var firstGuard = PidFileGuard.Acquire(first.Path);
        using var secondGuard = PidFileGuard.Acquire(second.Path);

        Assert.True(File.Exists(Path.Combine(first.Path, "moongate.pid")));
        Assert.True(File.Exists(Path.Combine(second.Path, "moongate.pid")));
    }

    [Fact]
    public void Acquire_PidPathIsDirectory_FailsAndReleasesLock()
    {
        using var directory = new TemporaryDirectory();
        var pidPath = Path.Combine(directory.Path, "moongate.pid");
        Directory.CreateDirectory(pidPath);

        Assert.Throws<UnauthorizedAccessException>(() => PidFileGuard.Acquire(directory.Path));

        Directory.Delete(pidPath);
        using var guard = PidFileGuard.Acquire(directory.Path);
        Assert.True(File.Exists(pidPath));
    }

    [Fact]
    public void Dispose_ChangedPid_PreservesOtherOwnersFile()
    {
        using var directory = new TemporaryDirectory();
        using var guard = PidFileGuard.Acquire(directory.Path);
        var pidPath = directory.CreateFile("moongate.pid", "12345-other-owner");

        guard.Dispose();

        Assert.Equal("12345-other-owner", File.ReadAllText(pidPath));
    }

    [Fact]
    public void Dispose_CalledAgainAfterNewAcquisition_DoesNotDeleteNewPid()
    {
        using var directory = new TemporaryDirectory();
        using var previous = PidFileGuard.Acquire(directory.Path);
        previous.Dispose();
        using var current = PidFileGuard.Acquire(directory.Path);

        previous.Dispose();

        Assert.Equal(Environment.ProcessId.ToString(CultureInfo.InvariantCulture),
            File.ReadAllText(Path.Combine(directory.Path, "moongate.pid")));
    }
}
