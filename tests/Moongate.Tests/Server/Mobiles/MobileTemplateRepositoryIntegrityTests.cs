using Moongate.Server.Loaders;
using Moongate.Server.Services.Items;
using Moongate.Server.Services.Mobiles;
using Moongate.Ultima.Types;
using Moongate.UO.Data.Types;
using SquidStd.Core.Directories;

namespace Moongate.Tests.Server.Mobiles;

public class MobileTemplateRepositoryIntegrityTests
{
    [Fact]
    public async Task ShippedTemplates_LoadResolveAndReferenceValidData()
    {
        var root = Path.Combine(Path.GetTempPath(), "moongate-mobile-integrity-" + Guid.NewGuid().ToString("N"));
        var directories = new DirectoriesConfig(root, []);
        var items = new ItemTemplateService();
        var mobiles = new MobileTemplateService();
        var loot = new LootTemplateService();
        var names = new NameService();

        try
        {
            await new ItemTemplatesLoader(items, directories).LoadAsync();

            // The mobile loader rejects a NamePool naming no registered pool, so the names have to
            // be loaded first — exactly as at boot, where NamesLoader runs at priority 30 and the
            // mobile templates at 150.
            await new NamesLoader(names, directories).LoadAsync();
            await new MobileTemplatesLoader(mobiles, directories, names).LoadAsync();
            await new LootTemplatesLoader(loot, items, directories).LoadAsync();

            Assert.NotEmpty(mobiles.All);

            // Gender parses from YAML into the enum where it is declared, and stays null where it is
            // not: the shipped female guard says Female, the male one says nothing at all and takes
            // male from the factory's default rather than from the template.
            Assert.Equal(MobileTemplateGenderType.Female, mobiles.GetById("warrior_guard_female_npc")!.Gender);
            Assert.Null(mobiles.GetById("warrior_guard_male_npc")!.Gender);

            foreach (var template in mobiles.All)
            {
                Assert.Null(template.BaseMobile); // every base_mobile is resolved at load

                foreach (var skill in template.Skills.Keys)
                {
                    var token = new string(skill.Where(char.IsLetter).ToArray());
                    Assert.True(
                        Enum.TryParse<SkillName>(token, true, out _),
                        $"Unknown skill '{skill}' in mobile template '{template.Id}'"
                    );
                }

                foreach (var entry in template.Equipment)
                {
                    Assert.True(
                        Enum.TryParse<LayerType>(entry.Layer, true, out _),
                        $"Unknown layer '{entry.Layer}' in mobile template '{template.Id}'"
                    );
                    Assert.True(
                        items.GetById(entry.Item) is not null,
                        $"Unknown item template '{entry.Item}' referenced by mobile template '{template.Id}'"
                    );
                }

                if (!string.IsNullOrEmpty(template.LootTableId))
                {
                    Assert.True(
                        loot.GetById(template.LootTableId) is not null,
                        $"Unknown loot table '{template.LootTableId}' referenced by mobile template '{template.Id}'"
                    );
                }

                foreach (var variant in template.Variants)
                {
                    if (!string.IsNullOrEmpty(variant.LootTableId))
                    {
                        Assert.True(
                            loot.GetById(variant.LootTableId) is not null,
                            $"Unknown loot table '{variant.LootTableId}' in variant '{variant.Name}' of mobile template '{template.Id}'"
                        );
                    }
                }
            }
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
