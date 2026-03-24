using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.FileLoaders;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Services.Templates;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.FileLoaders;

public sealed class LootTemplateLoaderTests
{
    [Test]
    public async Task LoadAsync_WhenRepositoryContainsGuardLootTables_ShouldLoadGuardLootDefinitions()
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var dataRoot = Path.Combine(repositoryRoot, "moongate_data");
        var directoriesConfig = new DirectoriesConfig(dataRoot, DirectoryType.Templates);
        var lootTemplateService = new LootTemplateService();
        var loader = new LootTemplateLoader(directoriesConfig, lootTemplateService);

        await loader.LoadAsync();

        Assert.Multiple(
            () =>
            {
                Assert.That(lootTemplateService.TryGet("guard.warrior", out var warriorLoot), Is.True);
                Assert.That(warriorLoot, Is.Not.Null);
                Assert.That(warriorLoot!.Mode, Is.EqualTo(LootTemplateMode.Additive));
                Assert.That(warriorLoot.Entries, Has.Count.EqualTo(1));
                Assert.That(warriorLoot.Entries[0].ItemTemplateId, Is.EqualTo("gold"));
                Assert.That(warriorLoot.Entries[0].AmountMin, Is.EqualTo(10));
                Assert.That(warriorLoot.Entries[0].AmountMax, Is.EqualTo(25));

                Assert.That(lootTemplateService.TryGet("guard.archer", out var archerLoot), Is.True);
                Assert.That(archerLoot, Is.Not.Null);
                Assert.That(archerLoot!.Mode, Is.EqualTo(LootTemplateMode.Additive));
                Assert.That(archerLoot.Entries, Has.Count.EqualTo(2));
                Assert.That(archerLoot.Entries[0].ItemTemplateId, Is.EqualTo("gold"));
                Assert.That(archerLoot.Entries[0].AmountMin, Is.EqualTo(10));
                Assert.That(archerLoot.Entries[0].AmountMax, Is.EqualTo(25));
                Assert.That(archerLoot.Entries[1].ItemTemplateId, Is.EqualTo("arrow"));
                Assert.That(archerLoot.Entries[1].Amount, Is.EqualTo(250));
            }
        );
    }

    [Test]
    public async Task LoadAsync_WhenRepositoryContainsVendorLootTables_ShouldLoadVendorLootDefinitions()
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var dataRoot = Path.Combine(repositoryRoot, "moongate_data");
        var directoriesConfig = new DirectoriesConfig(dataRoot, DirectoryType.Templates);
        var lootTemplateService = new LootTemplateService();
        var loader = new LootTemplateLoader(directoriesConfig, lootTemplateService);

        await loader.LoadAsync();

        Assert.Multiple(
            () =>
            {
                Assert.That(lootTemplateService.TryGet("vendor.blacksmith", out var blacksmithLoot), Is.True);
                Assert.That(blacksmithLoot, Is.Not.Null);
                Assert.That(blacksmithLoot!.Mode, Is.EqualTo(LootTemplateMode.Additive));
                Assert.That(blacksmithLoot.Entries, Has.Count.EqualTo(1));
                Assert.That(blacksmithLoot.Entries[0].ItemTemplateId, Is.EqualTo("gold"));
                Assert.That(blacksmithLoot.Entries[0].AmountMin, Is.EqualTo(5));
                Assert.That(blacksmithLoot.Entries[0].AmountMax, Is.EqualTo(15));

                Assert.That(lootTemplateService.TryGet("vendor.weaponsmith", out var weaponsmithLoot), Is.True);
                Assert.That(weaponsmithLoot, Is.Not.Null);
                Assert.That(weaponsmithLoot!.Mode, Is.EqualTo(LootTemplateMode.Additive));
                Assert.That(weaponsmithLoot.Entries, Has.Count.EqualTo(1));
                Assert.That(weaponsmithLoot.Entries[0].ItemTemplateId, Is.EqualTo("gold"));

                Assert.That(lootTemplateService.TryGet("vendor.armorer", out var armorerLoot), Is.True);
                Assert.That(armorerLoot, Is.Not.Null);
                Assert.That(armorerLoot!.Mode, Is.EqualTo(LootTemplateMode.Additive));
                Assert.That(armorerLoot.Entries, Has.Count.EqualTo(1));
                Assert.That(armorerLoot.Entries[0].ItemTemplateId, Is.EqualTo("gold"));
                Assert.That(armorerLoot.Entries[0].AmountMin, Is.EqualTo(5));
                Assert.That(armorerLoot.Entries[0].AmountMax, Is.EqualTo(15));

                Assert.That(lootTemplateService.TryGet("vendor.provisioner", out var provisionerLoot), Is.True);
                Assert.That(provisionerLoot, Is.Not.Null);
                Assert.That(provisionerLoot!.Mode, Is.EqualTo(LootTemplateMode.Additive));
                Assert.That(provisionerLoot.Entries, Has.Count.EqualTo(2));
                Assert.That(provisionerLoot.Entries[0].ItemTemplateId, Is.EqualTo("gold"));
                Assert.That(provisionerLoot.Entries[1].ItemTemplateId, Is.EqualTo("torch"));

                Assert.That(lootTemplateService.TryGet("vendor.mage", out var mageLoot), Is.True);
                Assert.That(mageLoot, Is.Not.Null);
                Assert.That(mageLoot!.Mode, Is.EqualTo(LootTemplateMode.Additive));
                Assert.That(mageLoot.Entries, Has.Count.EqualTo(1));
                Assert.That(mageLoot.Entries[0].ItemTemplateId, Is.EqualTo("gold"));

                Assert.That(lootTemplateService.TryGet("vendor.healer", out var healerLoot), Is.True);
                Assert.That(healerLoot, Is.Not.Null);
                Assert.That(healerLoot!.Mode, Is.EqualTo(LootTemplateMode.Additive));
                Assert.That(healerLoot.Entries, Has.Count.EqualTo(2));
                Assert.That(healerLoot.Entries[0].ItemTemplateId, Is.EqualTo("gold"));
                Assert.That(healerLoot.Entries[1].ItemTemplateId, Is.EqualTo("bandage"));
            }
        );
    }

    [Test]
    public async Task LoadAsync_WhenRepositoryContainsGeneratedLootFiles_ShouldLoadGeneratedModes()
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var dataRoot = Path.Combine(repositoryRoot, "moongate_data");
        var directoriesConfig = new DirectoriesConfig(dataRoot, DirectoryType.Templates);
        var lootTemplateService = new LootTemplateService();
        var loader = new LootTemplateLoader(directoriesConfig, lootTemplateService);

        await loader.LoadAsync();

        Assert.Multiple(
            () =>
            {
                Assert.That(lootTemplateService.TryGet("creature.agapite_elemental", out var creatureLoot), Is.True);
                Assert.That(creatureLoot, Is.Not.Null);
                Assert.That(creatureLoot!.Mode, Is.EqualTo(LootTemplateMode.Additive));

                Assert.That(lootTemplateService.TryGet("fillable.alchemist", out var fillableLoot), Is.True);
                Assert.That(fillableLoot, Is.Not.Null);
                Assert.That(fillableLoot!.Mode, Is.EqualTo(LootTemplateMode.Weighted));
                Assert.That(fillableLoot.Rolls, Is.GreaterThan(0));

                Assert.That(lootTemplateService.TryGet("treasure_map.level_1.gold", out var treasureLoot), Is.True);
                Assert.That(treasureLoot, Is.Not.Null);
                Assert.That(treasureLoot!.Mode, Is.EqualTo(LootTemplateMode.Additive));
            }
        );
    }

    [Test]
    public async Task LoadAsync_WhenRepositoryContainsBasePackTables_ShouldLoadThemWithExpectedModes()
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var dataRoot = Path.Combine(repositoryRoot, "moongate_data");
        var directoriesConfig = new DirectoriesConfig(dataRoot, DirectoryType.Templates);
        var lootTemplateService = new LootTemplateService();
        var loader = new LootTemplateLoader(directoriesConfig, lootTemplateService);

        await loader.LoadAsync();

        Assert.Multiple(
            () =>
            {
                Assert.That(lootTemplateService.TryGet("pack.low_scrolls", out var lowScrolls), Is.True);
                Assert.That(lowScrolls, Is.Not.Null);
                Assert.That(lowScrolls!.Mode, Is.EqualTo(LootTemplateMode.Additive));
                Assert.That(lowScrolls.Entries[0].ItemTemplateId, Is.EqualTo("clumsy_scroll"));

                Assert.That(lootTemplateService.TryGet("pack.med_scrolls", out var medScrolls), Is.True);
                Assert.That(medScrolls, Is.Not.Null);
                Assert.That(medScrolls!.Entries[0].ItemTemplateId, Is.EqualTo("arch_cure_scroll"));

                Assert.That(lootTemplateService.TryGet("pack.high_scrolls", out var highScrolls), Is.True);
                Assert.That(highScrolls, Is.Not.Null);
                Assert.That(highScrolls!.Entries[0].ItemTemplateId, Is.EqualTo("summon_air_elemental_scroll"));

                Assert.That(lootTemplateService.TryGet("pack.gems", out var gems), Is.True);
                Assert.That(gems, Is.Not.Null);
                Assert.That(gems!.Entries[0].ItemTemplateId, Is.EqualTo("amber"));

                Assert.That(lootTemplateService.TryGet("pack.potions", out var potions), Is.True);
                Assert.That(potions, Is.Not.Null);
                Assert.That(potions!.Mode, Is.EqualTo(LootTemplateMode.Weighted));
                Assert.That(potions.Rolls, Is.EqualTo(1));
                Assert.That(potions.Entries, Has.Count.EqualTo(6));

                Assert.That(lootTemplateService.TryGet("pack.poor", out var poor), Is.True);
                Assert.That(poor, Is.Not.Null);
                Assert.That(poor!.Mode, Is.EqualTo(LootTemplateMode.Additive));
                Assert.That(poor.Entries.Any(entry => entry.ItemTemplateId == "gold"), Is.True);

                Assert.That(lootTemplateService.TryGet("pack.meager", out var meager), Is.True);
                Assert.That(meager, Is.Not.Null);
                Assert.That(meager!.Mode, Is.EqualTo(LootTemplateMode.Additive));
                Assert.That(meager.Entries.Any(entry => entry.ItemTemplateId == "left_arm"), Is.True);

                Assert.That(lootTemplateService.TryGet("pack.average", out var average), Is.True);
                Assert.That(average, Is.Not.Null);
                Assert.That(average!.Mode, Is.EqualTo(LootTemplateMode.Additive));
                Assert.That(average.Entries.Any(entry => entry.ItemTemplateId == "amber"), Is.True);
            }
        );
    }

    [Test]
    public async Task LoadAsync_WhenTemplateDefinesAdditiveMode_ShouldLoadChanceAndAmountRange()
    {
        using var tempDirectory = new TempDirectory();
        var directoriesConfig = new DirectoriesConfig(
            tempDirectory.Path,
            DirectoryType.Data,
            DirectoryType.Templates,
            DirectoryType.Scripts,
            DirectoryType.Save,
            DirectoryType.Logs,
            DirectoryType.Cache
        );

        var lootDirectory = Path.Combine(directoriesConfig[DirectoryType.Templates], "loot");
        Directory.CreateDirectory(lootDirectory);

        var filePath = Path.Combine(lootDirectory, "creatures.json");
        await File.WriteAllTextAsync(
            filePath,
            """
            [
              {
                "type": "loot",
                "id": "undead.zombie",
                "name": "Zombie Loot",
                "category": "loot",
                "description": "",
                "mode": "additive",
                "entries": [
                  {
                    "itemTemplateId": "gold",
                    "chance": 0.5,
                    "amountMin": 20,
                    "amountMax": 50
                  }
                ]
              }
            ]
            """
        );

        var lootTemplateService = new LootTemplateService();
        var loader = new LootTemplateLoader(directoriesConfig, lootTemplateService);

        await loader.LoadAsync();

        Assert.That(lootTemplateService.TryGet("undead.zombie", out var template), Is.True);
        Assert.That(template, Is.Not.Null);

        Assert.Multiple(
            () =>
            {
                Assert.That(template!.Mode, Is.EqualTo(LootTemplateMode.Additive));
                Assert.That(template.Rolls, Is.EqualTo(1));
                Assert.That(template.NoDropWeight, Is.EqualTo(0));
                Assert.That(template.Entries, Has.Count.EqualTo(1));
                Assert.That(template.Entries[0].ItemTemplateId, Is.EqualTo("gold"));
                Assert.That(template.Entries[0].Chance, Is.EqualTo(0.5));
                Assert.That(template.Entries[0].AmountMin, Is.EqualTo(20));
                Assert.That(template.Entries[0].AmountMax, Is.EqualTo(50));
            }
        );
    }

    [Test]
    public async Task LoadAsync_WhenTemplateDefinesRollsAndItemTag_ShouldLoadThoseFields()
    {
        using var tempDirectory = new TempDirectory();
        var directoriesConfig = new DirectoriesConfig(
            tempDirectory.Path,
            DirectoryType.Data,
            DirectoryType.Templates,
            DirectoryType.Scripts,
            DirectoryType.Save,
            DirectoryType.Logs,
            DirectoryType.Cache
        );

        var lootDirectory = Path.Combine(directoriesConfig[DirectoryType.Templates], "loot");
        Directory.CreateDirectory(lootDirectory);

        var filePath = Path.Combine(lootDirectory, "undead.json");
        await File.WriteAllTextAsync(
            filePath,
            """
            [
              {
                "type": "loot",
                "id": "undead.low",
                "name": "Undead Low",
                "category": "loot",
                "description": "Undead corpse loot",
                "rolls": 2,
                "noDropWeight": 5,
                "entries": [
                  {
                    "weight": 10,
                    "itemTag": "weapon.rusty",
                    "amount": 1
                  }
                ]
              }
            ]
            """
        );

        var lootTemplateService = new LootTemplateService();
        var loader = new LootTemplateLoader(directoriesConfig, lootTemplateService);

        await loader.LoadAsync();

        Assert.That(lootTemplateService.TryGet("undead.low", out var template), Is.True);
        Assert.That(template, Is.Not.Null);

        Assert.Multiple(
            () =>
            {
                Assert.That(template!.Rolls, Is.EqualTo(2));
                Assert.That(template.NoDropWeight, Is.EqualTo(5));
                Assert.That(template.Entries, Has.Count.EqualTo(1));
                Assert.That(template.Entries[0].ItemTag, Is.EqualTo("weapon.rusty"));
                Assert.That(template.Entries[0].ItemTemplateId, Is.Null);
                Assert.That(template.Entries[0].ItemId, Is.Null);
            }
        );
    }

    private static string ResolveRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Moongate.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate repository root from test base directory.");
    }
}
