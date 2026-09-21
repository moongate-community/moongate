using Moongate.Migrations.Tests.TestSupport;
using Moongate.Persistence.Migrations.Services;
using Moongate.Persistence.Migrations.Types.Migrations;

namespace Moongate.Migrations.Tests.Catalog;

public sealed class MigrationHistoryTests
{
    [Fact]
    public void Validate_RejectsChecksumChangeBeforeReturningPending()
    {
        using var files = new MigrationFiles();
        files.Write("migrations/world/0001_first.sql", "SELECT 1;");
        var catalog = MigrationCatalog.Load(files.Core, null, MigrationTarget.World);
        Assert.Throws<InvalidOperationException>(
            () => MigrationHistory.Validate(
                catalog,
                [new(catalog.Scripts[0].Name, "changed")]
            )
        );
    }

    [Fact]
    public void Validate_RejectsMissingAppliedCoreFile()
    {
        using var files = new MigrationFiles();
        var catalog = MigrationCatalog.Load(files.Core, null, MigrationTarget.World);
        Assert.Throws<InvalidOperationException>(
            () => MigrationHistory.Validate(
                catalog,
                [new("core/0001_missing.sql", "checksum")]
            )
        );
    }

    [Fact]
    public void Validate_RejectsNewFileBeforeAppliedSequence()
    {
        using var files = new MigrationFiles();
        files.Write("migrations/world/0002_second.sql", "SELECT 2;");
        var before = MigrationCatalog.Load(files.Core, null, MigrationTarget.World);
        files.Write("migrations/world/0001_first.sql", "SELECT 1;");
        var after = MigrationCatalog.Load(files.Core, null, MigrationTarget.World);
        Assert.Throws<InvalidOperationException>(
            () => MigrationHistory.Validate(
                after,
                [new(before.Scripts[0].Name, before.Scripts[0].Checksum)]
            )
        );
    }

    [Fact]
    public void Validate_ReturnsOnlyPendingAndRetainsUninstalledPluginHistory()
    {
        using var files = new MigrationFiles();
        files.Write("migrations/world/0001_first.sql", "SELECT 1;");
        files.Write("migrations/world/0002_second.sql", "SELECT 2;");
        var catalog = MigrationCatalog.Load(files.Core, null, MigrationTarget.World);
        var pending = MigrationHistory.Validate(
            catalog,
            [new(catalog.Scripts[0].Name, catalog.Scripts[0].Checksum), new("removed/0001_old.sql", "old")]
        );
        Assert.Equal("core/0002_second.sql", Assert.Single(pending).Name);
    }
}
