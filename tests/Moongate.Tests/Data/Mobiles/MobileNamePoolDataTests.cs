using Moongate.Server.Loaders;
using Moongate.Server.Services.Mobiles;
using Moongate.UO.Data.Mobiles.Templates;
using SquidStd.Core.Directories;

namespace Moongate.Tests.Data.Mobiles;

/// <summary>
/// The shipped mobile templates against the shipped name pools. Both files are ours, so unlike the
/// tiledata invariants this is a real CI gate rather than a developer's check.
/// </summary>
public class MobileNamePoolDataTests
{
    [Fact]
    public async Task EveryDeclaredNamePool_ExistsInNamesYaml()
    {
        var (templates, names) = await Load();

        var unknown = new List<string>();

        foreach (var template in templates)
        {
            var pools = new[] { template.NamePool }.Concat(template.Variants.Select(variant => variant.NamePool));

            foreach (var pool in pools)
            {
                if (!string.IsNullOrEmpty(pool) && names.GetByType(pool) is null)
                {
                    unknown.Add($"{template.Id} -> {pool}");
                }
            }
        }

        Assert.True(unknown.Count == 0, $"Templates naming a pool that does not exist: {string.Join(", ", unknown)}");
    }

    // The whole point of the work: nothing that spawns should carry a blank label.
    [Fact]
    public async Task EveryShippedTemplate_ResolvesToAName()
    {
        var (templates, _) = await Load();

        var nameless = templates
                       .Where(template => template.Id != "base_human_npc")
                       .Where(template => template.Name.Length == 0 && template.NamePool.Length == 0)
                       .Select(template => template.Id)
                       .ToList();

        Assert.True(nameless.Count == 0, $"Templates that would spawn nameless: {string.Join(", ", nameless)}");
    }

    private static async Task<(IReadOnlyList<MobileTemplate> Templates, NameService Names)> Load()
    {
        var root = Path.Combine(Path.GetTempPath(), "mg-namepool-" + Guid.NewGuid().ToString("N"));
        var directories = new DirectoriesConfig(root, []);
        var names = new NameService();
        var templates = new MobileTemplateService();

        try
        {
            // Both loaders seed their embedded assets when the target directory is missing, which is
            // what makes a bare temp root enough to read the shipped data.
            await new NamesLoader(names, directories).LoadAsync();
            await new MobileTemplatesLoader(templates, directories, names).LoadAsync();

            return (templates.All, names);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
