using Moongate.Server.Data.Config.Sections;
using Moongate.Tests.TestSupport.Directories;

namespace Moongate.Tests.Server.Data.Config.Sections;

public sealed class SqlBackupConfigTests
{
    [Fact]
    public void ToOptions_Defaults_AreOffDailyFiveCopiesUnderTheRoot()
    {
        using var root = new TemporaryDirectory();

        var options = new SqlBackupConfig().ToOptions(root.Path);

        Assert.False(options.Enabled);
        Assert.Equal(TimeSpan.FromMinutes(1440), options.Interval);
        Assert.Equal(5, options.Keep);
        Assert.Equal(Path.Combine(Path.GetFullPath(root.Path), "backups"), options.Directory);
    }

    [Fact]
    public void ToOptions_AnAbsoluteDirectory_IsKept()
    {
        using var root = new TemporaryDirectory();
        using var elsewhere = new TemporaryDirectory();

        var options = new SqlBackupConfig { Directory = elsewhere.Path }.ToOptions(root.Path);

        Assert.Equal(Path.GetFullPath(elsewhere.Path), options.Directory);
    }

    [Fact]
    public void ToOptions_EnvironmentVariablesInTheDirectory_AreExpanded()
    {
        using var root = new TemporaryDirectory();
        var name = $"MOONGATE_TEST_BACKUP_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(name, "nightly");

        try
        {
            var options = new SqlBackupConfig { Directory = $"${{{name}}}/sql" }.ToOptions(root.Path);

            Assert.Equal(Path.Combine(Path.GetFullPath(root.Path), "nightly", "sql"), options.Directory);
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    [Theory, InlineData(0), InlineData(-1), InlineData(71583)]
    public void Validate_AnIntervalOutOfRange_IsRejected(int minutes)
    {
        var config = new SqlBackupConfig { IntervalMinutes = minutes };

        Assert.Throws<ArgumentOutOfRangeException>(config.Validate);
    }

    [Fact]
    public void Validate_TheLongestInterval_IsAccepted()
    {
        new SqlBackupConfig { IntervalMinutes = 71582 }.Validate();
    }

    [Theory, InlineData(0), InlineData(-3)]
    public void Validate_KeepBelowOne_IsRejected(int keep)
    {
        var config = new SqlBackupConfig { Keep = keep };

        Assert.Throws<ArgumentOutOfRangeException>(config.Validate);
    }

    [Theory, InlineData(""), InlineData("   ")]
    public void Validate_ABlankDirectory_IsRejected(string directory)
    {
        var config = new SqlBackupConfig { Directory = directory };

        Assert.Throws<ArgumentException>(config.Validate);
    }
}
