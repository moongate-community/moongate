using Lua;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Ultima.Data.Crafts;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Ultima.Modules;

/// <summary>
///     The <c>craft</c> Lua module: the crafts of <c>data/crafts</c>, their groups and recipes, and the resource lists the
///     recipes take from.
/// </summary>
[ScriptModule("craft", "The crafts of data/crafts: their groups and recipes, and the resource lists they use.")]
public sealed class CraftModule
{
    private readonly ICraftService _crafts;
    private readonly IItemTemplateService _templates;

    public CraftModule(ICraftService crafts, IItemTemplateService templates)
    {
        _crafts = crafts;
        _templates = templates;
    }

    /// <summary>
    ///     A craft as a table; <c>craft.get("carpentry")</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "The craft of that id as { id, name, skill, sound, groups }: groups is an array of { name, recipes }, a recipe { name, item, graphic, skill_min, skill_max, resources, skills }, resources an array of { resource, amount } (a list of craft.resource or an item template) and skills an array of { skill, min, max } besides the craft's own. graphic is the item's graphic, 0 when its template has none. Nil for an unknown id."
    )]
    public LuaTable? Get(string id)
    {
        if (_crafts.Get(id) is not { } craft)
        {
            return null;
        }

        var groups = new LuaTable();

        for (var index = 0; index < craft.Group.Count; index++)
        {
            groups[index + 1] = GroupTable(craft.Group[index]);
        }

        var table = new LuaTable();
        table["id"] = craft.Id;
        table["name"] = craft.Name;
        table["skill"] = craft.Skill;
        table["sound"] = craft.Sound;
        table["groups"] = groups;

        return table;
    }

    /// <summary>
    ///     The templates of a resource list; <c>craft.resource("wood")</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "The item templates that count for a resource list of data/crafts/resources.toml, such as 'wood', as an array of ids. Nil for an unknown list."
    )]
    public LuaTable? Resource(string id)
    {
        if (_crafts.Resource(id) is not { } templates)
        {
            return null;
        }

        var list = new LuaTable();

        for (var index = 0; index < templates.Count; index++)
        {
            list[index + 1] = templates[index];
        }

        return list;
    }

    private LuaTable GroupTable(CraftGroup group)
    {
        var recipes = new LuaTable();

        for (var index = 0; index < group.Recipe.Count; index++)
        {
            recipes[index + 1] = RecipeTable(group.Recipe[index]);
        }

        var table = new LuaTable();
        table["name"] = group.Name;
        table["recipes"] = recipes;

        return table;
    }

    private LuaTable RecipeTable(CraftRecipe recipe)
    {
        var resources = new LuaTable();

        for (var index = 0; index < recipe.Resources.Count; index++)
        {
            var resource = new LuaTable();
            resource["resource"] = recipe.Resources[index].Resource;
            resource["amount"] = recipe.Resources[index].Amount;
            resources[index + 1] = resource;
        }

        var skills = new LuaTable();

        for (var index = 0; index < recipe.Skills.Count; index++)
        {
            var skill = new LuaTable();
            skill["skill"] = recipe.Skills[index].Skill;
            skill["min"] = recipe.Skills[index].Min;
            skill["max"] = recipe.Skills[index].Max;
            skills[index + 1] = skill;
        }

        var table = new LuaTable();
        table["name"] = recipe.Name;
        table["item"] = recipe.Item;
        table["graphic"] = _templates.TryGet(recipe.Item, out var template) ? (int)template.ItemId.Value : 0;
        table["skill_min"] = recipe.SkillMin;
        table["skill_max"] = recipe.SkillMax;
        table["resources"] = resources;
        table["skills"] = skills;

        return table;
    }
}
