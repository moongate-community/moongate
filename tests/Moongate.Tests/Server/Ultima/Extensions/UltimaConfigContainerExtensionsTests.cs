using DryIoc;
using Moongate.Server.Core.Data.Config;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Extensions;
using Moongate.Tests.TestSupport.Directories;
using Tomlyn;
using Tomlyn.Model;

namespace Moongate.Tests.Server.Ultima.Extensions;

public sealed class UltimaConfigContainerExtensionsTests
{
    [Fact]
    public void AddUltimaConfig_ReadsEverySubTableAndRegistersIt()
    {
        using var directory = new TemporaryDirectory();
        var container = Container(
            directory,
            """
            [ultima]
            ultima_path = "/uo"

            [ultima.localization]
            language = "ita"

            [ultima.line_of_sight]
            max_distance = 20

            [ultima.world]
            view_range = 12

            [ultima.items]
            backpack_template = "pack"
            gold_template = "coin"

            [ultima.starting_items]
            best_skills = 2

            [ultima.characters]
            max_per_account = 5
            deletion_delay_hours = 48

            [ultima.npcs]
            think_interval_ms = 250
            sense_range = 6

            [ultima.crime]
            criminal_seconds = 300
            guards_enabled = false
            guard_template = "f_guard"
            guard_seconds = 15
            """
        );

        var ultima = container.AddUltimaConfig();

        var crime = container.Resolve<CrimeConfig>();
        Assert.Equal(
            (300, false, "f_guard", 15),
            (crime.CriminalSeconds, crime.GuardsEnabled, crime.GuardTemplate, crime.GuardSeconds)
        );
        Assert.Equal("/uo", container.Resolve<UltimaConfig>().UltimaPath);
        Assert.Same(ultima.World, container.Resolve<WorldConfig>());
        Assert.Equal("ita", container.Resolve<LocalizationConfig>().Language);
        Assert.Equal(20, container.Resolve<LineOfSightConfig>().MaxDistance);
        Assert.Equal(12, container.Resolve<WorldConfig>().ViewRange);
        Assert.Equal(
            ("pack", "coin"),
            (container.Resolve<ItemsConfig>().BackpackTemplate, container.Resolve<ItemsConfig>().GoldTemplate)
        );
        Assert.Equal(2, container.Resolve<StartingItemsConfig>().BestSkills);
        Assert.Equal(
            (5, 48),
            (container.Resolve<CharactersConfig>().MaxPerAccount, container.Resolve<CharactersConfig>().DeletionDelayHours)
        );
        Assert.Equal(
            (250, 6),
            (container.Resolve<NpcsConfig>().ThinkIntervalMs, container.Resolve<NpcsConfig>().SenseRange)
        );
    }

    [Fact]
    public void AddUltimaConfig_NoUltimaSection_AppendsTheDefaults()
    {
        using var directory = new TemporaryDirectory();
        var container = Container(directory, "[network]\ngame_port = 4001\n");

        container.AddUltimaConfig();

        var document =
            TomlSerializer.Deserialize<TomlTable>(File.ReadAllText(Path.Combine(directory.Path, "moongate.toml")))!;
        var ultima = Assert.IsType<TomlTable>(document["ultima"]);
        Assert.Equal(18L, Assert.IsType<TomlTable>(ultima["world"])["view_range"]);
        Assert.False(Assert.IsType<TomlTable>(ultima["starting_items"]).ContainsKey("gold"));
    }

    [Fact]
    public void AddUltimaConfig_AnInvalidSubTable_Stops()
    {
        using var directory = new TemporaryDirectory();
        var container = Container(directory, "[ultima.world]\nview_range = 2\n");

        Assert.Throws<InvalidOperationException>(() => container.AddUltimaConfig());
    }

    private static Container Container(TemporaryDirectory directory, string toml)
    {
        var path = directory.CreateFile("moongate.toml", toml);
        var container = new Container();
        container.RegisterInstance(
            new ServerConfigDocument(path, TomlSerializer.Deserialize<TomlTable>(toml)!, ["network"])
        );

        return container;
    }
}
