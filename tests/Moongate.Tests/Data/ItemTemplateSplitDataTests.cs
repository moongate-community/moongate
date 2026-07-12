using Moongate.UO.Data.Items;
using SquidStd.Core.Yaml;

namespace Moongate.Tests.Data;

public class ItemTemplateSplitDataTests
{
    private static readonly IReadOnlyDictionary<string, int> ExpectedCounts =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["addons.yaml"] = 9, ["aquarium.yaml"] = 35, ["armor.yaml"] = 143,
            ["base/books.yaml"] = 2, ["base/doors.yaml"] = 20, ["base/dye.yaml"] = 1,
            ["base/keys.yaml"] = 1, ["base/royalty.yaml"] = 2, ["base/signs.yaml"] = 1,
            ["base/spawners.yaml"] = 1, ["base/static.yaml"] = 1,
            ["base/teleports.yaml"] = 10, ["base/test_containers.yaml"] = 1,
            ["body_parts.yaml"] = 6, ["books.yaml"] = 38, ["bulletin_boards.yaml"] = 2,
            ["champion_artifacts.yaml"] = 13, ["clothing.yaml"] = 71,
            ["construction.yaml"] = 128, ["containers.yaml"] = 22,
            ["decoration_artifacts.yaml"] = 77, ["deeds.yaml"] = 8, ["food.yaml"] = 136,
            ["games.yaml"] = 5, ["gems.yaml"] = 9, ["gm/gm_body.yaml"] = 8,
            ["guilds.yaml"] = 3, ["jewels.yaml"] = 12, ["lights.yaml"] = 25,
            ["loot_support.yaml"] = 3, ["maps.yaml"] = 2, ["minor_artifacts.yaml"] = 10,
            ["misc.yaml"] = 160, ["mounts.yaml"] = 1,
            ["new_haven_quest_rewards.yaml"] = 2, ["plants_flowers.yaml"] = 13,
            ["resources.yaml"] = 63, ["resurrection.yaml"] = 4, ["shields.yaml"] = 10,
            ["skill_items.yaml"] = 234, ["special.yaml"] = 190, ["suits.yaml"] = 8,
            ["talismans.yaml"] = 5, ["test.yaml"] = 1, ["training_items.yaml"] = 7,
            ["traps.yaml"] = 5, ["treasure_chests.yaml"] = 4, ["wands.yaml"] = 11,
            ["weapons.yaml"] = 141
        };

    [Fact]
    public void SplitTemplates_HaveExpectedFilesCountsAndMemberships()
    {
        var repositoryRoot = FindRepositoryRoot();
        var splitRoot = Path.Combine(repositoryRoot, "src", "Moongate.Server", "Assets", "Templates", "Items");
        var splitFiles = Directory.GetFiles(splitRoot, "*.yaml", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var splitByFile = splitFiles.ToDictionary(
            path => Path.GetRelativePath(splitRoot, path).Replace(Path.DirectorySeparatorChar, '/'),
            path => YamlUtils.DeserializeFromFile<ItemTemplate[]>(path) ?? [],
            StringComparer.OrdinalIgnoreCase);
        var splitItems = splitByFile.Values.SelectMany(items => items).ToArray();

        Assert.Equal(49, splitFiles.Length);
        Assert.Equal(1664, splitItems.Length);
        Assert.Equal(1664, splitItems.Select(item => item.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());

        Assert.Equal(
            ExpectedCounts.Keys.OrderBy(path => path, StringComparer.OrdinalIgnoreCase),
            splitByFile.Keys.OrderBy(path => path, StringComparer.OrdinalIgnoreCase));

        foreach (var (path, expectedCount) in ExpectedCounts)
        {
            Assert.Equal(expectedCount, splitByFile[path].Length);
        }

        Assert.Contains(splitByFile["food.yaml"], item => item.Id == "apple");
        Assert.Contains(splitByFile["base/static.yaml"], item => item.Id == "static");
        Assert.Contains(splitByFile["gm/gm_body.yaml"], item => item.Id == "gm_body_bag");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (directory is not null)
        {
            if (Directory.Exists(
                    Path.Combine(directory.FullName, "src", "Moongate.Server", "Assets", "Templates", "Items")
                ))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
