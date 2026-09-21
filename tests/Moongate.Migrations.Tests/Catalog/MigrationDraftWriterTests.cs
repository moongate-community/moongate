using Moongate.Migrations.Tests.TestSupport;
using Moongate.Persistence.Migrations.Services;
using Moongate.Persistence.Migrations.Types.Migrations;

namespace Moongate.Migrations.Tests.Catalog;

public sealed class MigrationDraftWriterTests
{
    [Fact]
    public async Task WriteAsync_NumberingIsPerTargetAndNeverOverwrites()
    {
        using var files = new MigrationFiles();
        files.Write("migrations/auth/0007_existing.sql", "SELECT 7;");
        var catalog = MigrationCatalog.Load(files.Core, files.Plugins, MigrationTarget.Auth);
        var path = await MigrationDraftWriter.WriteAsync(catalog, "core", "SELECT 8;", false);
        Assert.EndsWith("0008_auto_schema.sql", path);
        Assert.Equal("SELECT 7;", File.ReadAllText(Path.Combine(files.Core, "auth/0007_existing.sql")));
        await Assert.ThrowsAsync<IOException>(() => MigrationDraftWriter.WriteAsync(catalog, "core", "SELECT 9;", false));
        Assert.Contains("SELECT 8;", File.ReadAllText(path));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.tmp"));
        var world = await MigrationDraftWriter.WriteAsync(
            MigrationCatalog.Load(files.Core, null, MigrationTarget.World),
            "core",
            "SELECT 1;",
            false
        );
        Assert.EndsWith("0001_auto_schema.sql", world);
    }

    [Fact]
    public async Task WriteAsync_ReviewRequiredSurvivesReload()
    {
        using var files = new MigrationFiles();
        await MigrationDraftWriter.WriteAsync(
            MigrationCatalog.Load(files.Core, null, MigrationTarget.World),
            "core",
            "DROP TABLE sample;",
            true
        );
        var script = Assert.Single(MigrationCatalog.Load(files.Core, null, MigrationTarget.World).Scripts);
        Assert.Throws<InvalidOperationException>(() => MigrationReviewGuard.Validate([script]));
    }

    [Fact]
    public async Task WriteAsync_ExhaustedSequenceAndEmptySqlAreRejected()
    {
        using var files = new MigrationFiles();
        files.Write("migrations/auth/9999_last.sql", "SELECT 1;");
        var catalog = MigrationCatalog.Load(files.Core, null, MigrationTarget.Auth);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            MigrationDraftWriter.WriteAsync(catalog, "core", "SELECT 2;", false)
        );
        await Assert.ThrowsAsync<ArgumentException>(() => MigrationDraftWriter.WriteAsync(catalog, "core", " ", false));
    }

    [Fact]
    public async Task WriteAsync_PluginUsesManifestComponentRoot()
    {
        using var files = new MigrationFiles();
        files.Write("plugins/Notes/migrations/manifest.json", "{\"id\":\"notes\"}");
        var path = await MigrationDraftWriter.WriteAsync(
            MigrationCatalog.Load(files.Core, files.Plugins, MigrationTarget.World),
            "notes",
            "SELECT 1;",
            false
        );
        Assert.Equal(Path.Combine(files.Plugins, "Notes/migrations/world/0001_auto_schema.sql"), path);
        Assert.Empty(Directory.GetFiles(Path.Combine(files.Core, "world")));
    }
}
