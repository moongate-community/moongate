using System.Text;
using Moongate.Server.Bootstrap.Internal;

namespace Moongate.Tests.Server.Bootstrap;

[TestFixture]
public class PidFileGuardTests
{
    private string _tempDirectory = null!;

    [Test]
    public void Acquire_ShouldCreatePidFile_AndDeleteOnDispose()
    {
        var pidFile = Path.Combine(_tempDirectory, "moongate.pid");

        using (PidFileGuard.Acquire(_tempDirectory, () => 1234, static _ => false))
        {
            Assert.That(File.Exists(pidFile), Is.True);
            Assert.That(File.ReadAllText(pidFile).Trim(), Is.EqualTo("1234"));
        }

        Assert.That(File.Exists(pidFile), Is.False);
    }

    [Test]
    public void Acquire_ShouldHonorPidFileWrittenWithUtf8Bom()
    {
        var pidFile = Path.Combine(_tempDirectory, "moongate.pid");
        File.WriteAllText(pidFile, "9999", new UTF8Encoding(true));

        var ex = Assert.Throws<InvalidOperationException>(
            () => PidFileGuard.Acquire(_tempDirectory, () => 1234, static pid => pid == 9999)
        );

        Assert.That(ex!.Message, Does.Contain("PID 9999"));
    }

    [Test]
    public void Acquire_ShouldOverwriteStalePidFile()
    {
        var pidFile = Path.Combine(_tempDirectory, "moongate.pid");
        File.WriteAllText(pidFile, "9999");

        using var _ = PidFileGuard.Acquire(_tempDirectory, () => 1234, static _ => false);

        Assert.That(File.ReadAllText(pidFile).Trim(), Is.EqualTo("1234"));
    }

    [Test]
    public void Acquire_ShouldThrow_WhenAnotherLiveProcessOwnsPidFile()
    {
        File.WriteAllText(Path.Combine(_tempDirectory, "moongate.pid"), "9999");

        var ex = Assert.Throws<InvalidOperationException>(
            () => PidFileGuard.Acquire(_tempDirectory, () => 1234, static pid => pid == 9999)
        );

        Assert.That(ex!.Message, Does.Contain("PID 9999"));
    }

    [Test]
    public void Dispose_ShouldNotDeletePidFile_WhenOwnershipChanged()
    {
        var pidFile = Path.Combine(_tempDirectory, "moongate.pid");
        var guard = PidFileGuard.Acquire(_tempDirectory, () => 1234, static _ => false);
        File.WriteAllText(pidFile, "5678");

        guard.Dispose();

        Assert.That(File.Exists(pidFile), Is.True);
        Assert.That(File.ReadAllText(pidFile).Trim(), Is.EqualTo("5678"));
    }

    [SetUp]
    public void SetUp()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "moongate-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }
}
