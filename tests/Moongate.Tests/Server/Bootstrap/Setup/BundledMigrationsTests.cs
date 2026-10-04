using Moongate.Server.Bootstrap.Internal.Setup;
using Moongate.Tests.TestSupport.Directories;

namespace Moongate.Tests.Server.Bootstrap.Setup;

public sealed class BundledMigrationsTests
{
    [Fact]
    public void CopyMissing_AddsTheFilesTheDestinationLacks_AndKeepsTheOnesItHas()
    {
        using var directory = new TemporaryDirectory();
        var source = CreateSource(directory);
        var destination = Path.Combine(directory.Path, "root/migrations");
        Directory.CreateDirectory(Path.Combine(destination, "world"));
        File.WriteAllText(Path.Combine(destination, "world/0001_base.sql"), "SELECT 1;\n");
        File.WriteAllText(Path.Combine(destination, "world/0003_auto_schema.sql"), "SELECT 3;\n");

        var created = BundledMigrations.CopyMissing(source, destination);

        Assert.Equal(
            [Path.Combine(destination, "auth/0001_accounts.sql"), Path.Combine(destination, "world/0002_more.sql")],
            created
        );
        Assert.Equal("SELECT 2;\n", File.ReadAllText(Path.Combine(destination, "world/0002_more.sql")));
        // What the root has beyond the distribution stays.
        Assert.True(File.Exists(Path.Combine(destination, "world/0003_auto_schema.sql")));

        Assert.Empty(BundledMigrations.CopyMissing(source, destination));
    }

    [Theory, InlineData("0002_more.sql", "SELECT 99;\n"), InlineData("0002_auto_schema.sql", "SELECT 2;\n")]
    public void CopyMissing_AFileOfTheRootWithTheSameNumberAndAnotherNameOrContent_StopsAndCopiesNothing(
        string name,
        string sql
    )
    {
        using var directory = new TemporaryDirectory();
        var source = CreateSource(directory);
        var destination = Path.Combine(directory.Path, "root/migrations");
        Directory.CreateDirectory(Path.Combine(destination, "world"));
        File.WriteAllText(Path.Combine(destination, "world", name), sql);

        var exception = Assert.Throws<InvalidOperationException>(() => BundledMigrations.CopyMissing(source, destination));

        Assert.Contains(name, exception.Message);
        Assert.Equal(sql, File.ReadAllText(Path.Combine(destination, "world", name)));
        Assert.False(File.Exists(Path.Combine(destination, "world/0001_base.sql")));
        Assert.False(Directory.Exists(Path.Combine(destination, "auth")));
    }

    [Fact]
    public void CopyMissing_AHalfWrittenFileOfAStartThatWasKilled_IsWrittenAgain_AndLeavesNoTemporaryFile()
    {
        using var directory = new TemporaryDirectory();
        var source = CreateSource(directory);
        var destination = Path.Combine(directory.Path, "root/migrations");
        Directory.CreateDirectory(Path.Combine(destination, "world"));
        File.WriteAllText(Path.Combine(destination, "world/0002_more.sql.tmp"), "SELE");

        BundledMigrations.CopyMissing(source, destination);

        Assert.Equal("SELECT 2;\n", File.ReadAllText(Path.Combine(destination, "world/0002_more.sql")));
        Assert.Empty(Directory.GetFiles(destination, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public void CopyMissing_WithNoBundledMigrations_CopiesNothing()
    {
        using var directory = new TemporaryDirectory();
        var destination = Path.Combine(directory.Path, "root/migrations");

        Assert.Empty(BundledMigrations.CopyMissing(Path.Combine(directory.Path, "missing"), destination));
        Assert.False(Directory.Exists(destination));
    }

    private static string CreateSource(TemporaryDirectory directory)
    {
        var source = Path.Combine(directory.Path, "distribution-migrations");
        Directory.CreateDirectory(Path.Combine(source, "auth"));
        Directory.CreateDirectory(Path.Combine(source, "world"));
        File.WriteAllText(Path.Combine(source, "auth/0001_accounts.sql"), "SELECT 1;\n");
        File.WriteAllText(Path.Combine(source, "world/0001_base.sql"), "SELECT 1;\n");
        File.WriteAllText(Path.Combine(source, "world/0002_more.sql"), "SELECT 2;\n");

        return source;
    }
}
