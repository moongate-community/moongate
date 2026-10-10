using Moongate.Core.Directories;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data;
using Moongate.Server.Ultima.Data.Crafts;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Server.Ultima.Utils;
using Moongate.Ultima.Types;
using Serilog;

namespace Moongate.Server.Ultima.Loaders;

/// <summary>
///     Loads the crafts of <c>data/crafts</c>, one a file, all but <c>resources.toml</c>. The folder may be missing: no
///     craft then. Skill names are kept in the snake_case form scripts read them by. A bad or repeated id, a craft
///     without a name or groups, a group without recipes, an unknown skill, a group or recipe without a name, an item or resource that is
///     neither an item template nor a resource list, an amount below 1, a recipe without resources, a spell that is
///     no key or a mana below 0, or skill bounds outside 0 to 150 (the least of the main skill from -50) or the least above the most stop the server at startup, naming the file.
/// </summary>
public class CraftsLoader : IDataLoader<CraftDefinition>
{
    private const double MaxSkill = 150;
    private const double MinSkill = -50;
    private const string ResourcesFile = "resources.toml";

    private readonly ILogger _logger = Log.ForContext<CraftsLoader>();

    private readonly DirectoriesConfig _directoriesConfig;
    private readonly IDataLoaderService _dataLoaderService;

    private string craftsDirectoryPath => Path.Join(_directoriesConfig["data"], "crafts");

    public CraftsLoader(DirectoriesConfig directoriesConfig, IDataLoaderService dataLoaderService)
    {
        _directoriesConfig = directoriesConfig;
        _dataLoaderService = dataLoaderService;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async Task<DataLoaderResult<CraftDefinition>> LoadDataAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(craftsDirectoryPath))
        {
            _logger.Information("No data/crafts folder: nothing can be crafted");

            return new() { Entities = [] };
        }

        var items = _dataLoaderService.GetEntities<ItemTemplate>()
            .Select(template => template.Id)
            .ToHashSet(StringComparer.Ordinal);
        var lists = _dataLoaderService.GetEntities<CraftResourceList>()
            .Select(list => list.Id)
            .ToHashSet(StringComparer.Ordinal);
        var crafts = new Dictionary<string, CraftDefinition>(StringComparer.Ordinal);

        foreach (var path in Directory.EnumerateFiles(craftsDirectoryPath, "*.toml")
                     .Where(path => Path.GetFileName(path) != ResourcesFile)
                     .Order(StringComparer.Ordinal))
        {
            var craft = await TomlUtils.DeserializeFromFileAsync<CraftDefinition>(path, null, cancellationToken) ??
                        new CraftDefinition();
            Check(craft, path, items, lists);

            if (!crafts.TryAdd(craft.Id, craft))
            {
                throw new InvalidDataException($"{path}: the craft {craft.Id} is in another file too.");
            }
        }

        _logger.Information("Found {Count} crafts", crafts.Count);

        return new() { Entities = crafts.Values.ToList() };
    }

    private static void Check(CraftDefinition craft, string path, HashSet<string> items, HashSet<string> lists)
    {
        if (!ScriptIdUtils.IsValid(craft.Id))
        {
            throw Invalid(path, $"the craft id '{craft.Id}' {ScriptIdUtils.Rule}");
        }

        if (string.IsNullOrWhiteSpace(craft.Name))
        {
            throw Invalid(path, $"the craft {craft.Id} has no name");
        }

        craft.Skill = SkillName(craft.Skill) ?? throw Invalid(path, $"the skill '{craft.Skill}' is no skill");

        if (craft.Group.Count == 0)
        {
            throw Invalid(path, $"the craft {craft.Id} has no group");
        }

        foreach (var group in craft.Group)
        {
            if (string.IsNullOrWhiteSpace(group.Name))
            {
                throw Invalid(path, "a group has no name");
            }

            if (group.Recipe.Count == 0)
            {
                throw Invalid(path, $"the group {group.Name} has no recipe");
            }

            foreach (var recipe in group.Recipe)
            {
                CheckRecipe(recipe, path, items, lists);
            }
        }
    }

    private static void CheckRecipe(CraftRecipe recipe, string path, HashSet<string> items, HashSet<string> lists)
    {
        if (string.IsNullOrWhiteSpace(recipe.Name))
        {
            throw Invalid(path, $"a recipe making '{recipe.Item}' has no name");
        }

        var where = $"the recipe {recipe.Name}";

        if (!items.Contains(recipe.Item))
        {
            throw Invalid(path, $"{where} makes '{recipe.Item}', which is not an item template");
        }

        if (recipe.SkillMin < MinSkill || !AreBounds(Math.Max(recipe.SkillMin, 0), recipe.SkillMax) || recipe.SkillMin > recipe.SkillMax)
        {
            throw Invalid(path, $"{where} has skill bounds outside {MinSkill} to {MaxSkill} or the least above the most");
        }

        if (recipe.Mana < 0)
        {
            throw Invalid(path, $"{where} takes {recipe.Mana} mana");
        }

        if (recipe.Spell.Length > 0 && !ScriptIdUtils.IsValid(recipe.Spell))
        {
            throw Invalid(path, $"{where} names the spell '{recipe.Spell}', which {ScriptIdUtils.Rule}");
        }

        if (recipe.Resources.Count == 0)
        {
            throw Invalid(path, $"{where} takes nothing");
        }

        foreach (var resource in recipe.Resources)
        {
            if (!lists.Contains(resource.Resource) && !items.Contains(resource.Resource))
            {
                throw Invalid(
                    path,
                    $"{where} takes '{resource.Resource}', which is neither a resource list nor an item template"
                );
            }

            if (resource.Amount < 1)
            {
                throw Invalid(path, $"{where} takes {resource.Amount} of {resource.Resource}");
            }
        }

        foreach (var skill in recipe.Skills)
        {
            skill.Skill = (AreBounds(skill.Min, skill.Max) ? SkillName(skill.Skill) : null) ??
                          throw Invalid(path, $"{where} asks for the skill '{skill.Skill}' with bad bounds or no such skill");
        }
    }

    // The skill as scripts read it from mobile.skills, such as animal_lore; null for no skill.
    private static string? SkillName(string name)
    {
        return EnumNameUtils.TryParse<SkillType>(name, out var skill) && Enum.IsDefined(skill)
            ? EnumNameUtils.Format(skill)
            : null;
    }

    private static bool AreBounds(double min, double max)
    {
        return min is >= 0 and <= MaxSkill && max is >= 0 and <= MaxSkill && min <= max;
    }

    private static InvalidDataException Invalid(string path, string reason)
    {
        return new($"{path}: {reason}.");
    }
}
