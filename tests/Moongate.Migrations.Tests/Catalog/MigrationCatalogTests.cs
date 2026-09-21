using System.Text.Json;
using Moongate.Migrations.Tests.TestSupport;
using Moongate.Persistence.Migrations.Services;
using Moongate.Persistence.Migrations.Types.Migrations;

namespace Moongate.Migrations.Tests.Catalog;

public sealed class MigrationCatalogTests
{
    [Fact]
    public void Load_NormalizesLineEndingsForPortableChecksums()
    {
        using var files = new MigrationFiles();
        files.Write("migrations/world/0001_first.sql", "SELECT 1;\nSELECT 2;\n");
        var first = MigrationCatalog.Load(files.Core, null, MigrationTarget.World).Scripts[0];
        files.Write("migrations/world/0001_first.sql", "\ufeffSELECT 1;\r\nSELECT 2;\r\n");
        var second = MigrationCatalog.Load(files.Core, null, MigrationTarget.World).Scripts[0];
        Assert.Equal(first.Checksum, second.Checksum);
    }

    [Fact]
    public void Load_OrdersCoreBeforePluginsAndIsolatesTargets()
    {
        using var files = new MigrationFiles();
        files.Write("migrations/world/0002_second.sql", "SELECT 2;");
        files.Write("migrations/world/0001_first.sql", "SELECT 1;");
        files.Write("migrations/auth/0001_auth.sql", "SELECT 3;");
        files.Write("plugins/renamable/migrations/manifest.json", "{\"id\":\"guilds\"}");
        files.Write("plugins/renamable/migrations/world/0001_guilds.sql", "SELECT 4;");
        var catalog = MigrationCatalog.Load(files.Core, files.Plugins, MigrationTarget.World);
        Assert.Equal(
            new[] { "core/0001_first.sql", "core/0002_second.sql", "guilds/0001_guilds.sql" },
            catalog.Scripts.Select(s => s.Name)
        );
    }

    [Theory, InlineData("../evil"), InlineData("core"), InlineData("Guilds"), InlineData("")]
    public void Load_RejectsInvalidComponentIds(string id)
    {
        using var files = new MigrationFiles();
        files.Write("plugins/p/migrations/manifest.json", JsonSerializer.Serialize(new { id }));
        Assert.Throws<InvalidOperationException>(
            () => MigrationCatalog.Load(
                files.Core,
                files.Plugins,
                MigrationTarget.World
            )
        );
    }

    [Theory, InlineData("0000_invalid.sql"), InlineData("1_invalid.sql"), InlineData("0001_Upper.sql"),
     InlineData("0001_same.sql")]
    public void Load_RejectsInvalidOrDuplicateSequences(string name)
    {
        using var files = new MigrationFiles();
        files.Write("migrations/world/0001_first.sql", "SELECT 1;");
        files.Write("migrations/world/" + name, "SELECT 2;");
        Assert.Throws<InvalidOperationException>(
            () => MigrationCatalog.Load(
                files.Core,
                files.Plugins,
                MigrationTarget.World
            )
        );
    }

    [Fact]
    public void Load_RequiresManifestForPluginSql()
    {
        using var files = new MigrationFiles();
        files.Write("plugins/p/migrations/world/0001_first.sql", "SELECT 1;");
        Assert.Throws<InvalidOperationException>(
            () => MigrationCatalog.Load(
                files.Core,
                files.Plugins,
                MigrationTarget.World
            )
        );
    }
}
