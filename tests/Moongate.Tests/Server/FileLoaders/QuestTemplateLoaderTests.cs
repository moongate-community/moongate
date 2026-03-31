using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.FileLoaders;
using Moongate.Server.Services.Scripting;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Services.Templates;
namespace Moongate.Tests.Server.FileLoaders;

public sealed class QuestTemplateLoaderTests
{
    [Test]
    public async Task LoadAsync_WhenQuestLuaFilesAreValid_ShouldCompileQuestTemplates()
    {
        using var tempDirectory = new TempDirectory();
        var directoriesConfig = CreateDirectoriesConfig(tempDirectory.Path);
        var questsDirectory = Path.Combine(directoriesConfig[DirectoryType.Scripts], "quests", "new_haven");
        Directory.CreateDirectory(questsDirectory);

        await File.WriteAllTextAsync(
            Path.Combine(questsDirectory, "rat_hunt.lua"),
            """
            quest.define({
                id = "new_haven.rat_hunt",
                name = "Rat Hunt",
                category = "starter",
                description = "Cull the rat infestation near the mill.",
                quest_givers = { "farmer_npc" },
                completion_npcs = { "farmer_npc" },
                repeatable = false,
                max_active_per_character = 1,
                objectives = {
                    quest.kill({ mobiles = { "sewer_rat", "giant_rat" }, amount = 10 }),
                    quest.collect({ item_template_id = "rat_tail", amount = 10 }),
                    quest.deliver({ item_template_id = "rat_tail", amount = 10 })
                },
                rewards = {
                    quest.gold(150),
                    quest.item("bandage", 10)
                }
            })
            """
        );

        var definitionService = new QuestDefinitionService();
        var templateService = new QuestTemplateService();
        var loader = new QuestTemplateLoader(directoriesConfig, definitionService, templateService);

        await loader.LoadAsync();

        Assert.That(templateService.TryGet("new_haven.rat_hunt", out var template), Is.True);
        Assert.That(template, Is.Not.Null);

        Assert.Multiple(
            () =>
            {
                Assert.That(template!.QuestGiverTemplateIds, Is.EqualTo(new[] { "farmer_npc" }));
                Assert.That(template.CompletionNpcTemplateIds, Is.EqualTo(new[] { "farmer_npc" }));
                Assert.That(template.Objectives, Has.Count.EqualTo(3));
                Assert.That(template.Rewards, Has.Count.EqualTo(1));
                Assert.That(template.Rewards[0].Gold, Is.EqualTo(150));
                Assert.That(template.Rewards[0].Items[0].ItemTemplateId, Is.EqualTo("bandage"));
                Assert.That(definitionService.TryGet("new_haven.rat_hunt", out var definition), Is.True);
                Assert.That(definition, Is.Not.Null);
                Assert.That(definition!.ScriptPath, Does.EndWith("scripts/quests/new_haven/rat_hunt.lua"));
            }
        );
    }

    [Test]
    public async Task LoadSingleAsync_WhenQuestFileWasDeleted_ShouldRemoveQuestTemplateFromCache()
    {
        using var tempDirectory = new TempDirectory();
        var directoriesConfig = CreateDirectoriesConfig(tempDirectory.Path);
        var questsDirectory = Path.Combine(directoriesConfig[DirectoryType.Scripts], "quests");
        Directory.CreateDirectory(questsDirectory);

        var ratHuntPath = Path.Combine(questsDirectory, "rat_hunt.lua");
        var spiderCullPath = Path.Combine(questsDirectory, "spider_cull.lua");

        await File.WriteAllTextAsync(ratHuntPath, BuildQuestScript("new_haven.rat_hunt", "Rat Hunt", "sewer_rat"));
        await File.WriteAllTextAsync(spiderCullPath, BuildQuestScript("new_haven.spider_cull", "Spider Cull", "giant_spider"));

        var definitionService = new QuestDefinitionService();
        var templateService = new QuestTemplateService();
        var loader = new QuestTemplateLoader(directoriesConfig, definitionService, templateService);

        await loader.LoadAsync();
        File.Delete(spiderCullPath);

        await loader.LoadSingleAsync(spiderCullPath);

        Assert.Multiple(
            () =>
            {
                Assert.That(templateService.TryGet("new_haven.rat_hunt", out var ratHunt), Is.True);
                Assert.That(ratHunt, Is.Not.Null);
                Assert.That(ratHunt!.Name, Is.EqualTo("Rat Hunt"));
                Assert.That(ratHunt.Objectives[0].MobileTemplateIds, Is.EqualTo(new[] { "sewer_rat" }));
                Assert.That(templateService.TryGet("new_haven.spider_cull", out _), Is.False);
                Assert.That(templateService.Count, Is.EqualTo(1));
            }
        );
    }

    [Test]
    public void LoadAsync_WhenQuestScriptHasSyntaxError_ShouldThrow()
    {
        using var tempDirectory = new TempDirectory();
        var directoriesConfig = CreateDirectoriesConfig(tempDirectory.Path);
        var questsDirectory = Path.Combine(directoriesConfig[DirectoryType.Scripts], "quests");
        Directory.CreateDirectory(questsDirectory);

        File.WriteAllText(
            Path.Combine(questsDirectory, "broken.lua"),
            """
            quest.define({
                id = "broken.quest"
            """
        );

        var loader = new QuestTemplateLoader(
            directoriesConfig,
            new QuestDefinitionService(),
            new QuestTemplateService()
        );

        Assert.That(
            async () => await loader.LoadAsync(),
            Throws.TypeOf<InvalidOperationException>().With.Message.Contains("broken.lua")
        );
    }

    private static string BuildQuestScript(string id, string name, string mobileTemplateId)
        => $$"""
           quest.define({
               id = "{{id}}",
               name = "{{name}}",
               category = "starter",
               description = "test quest",
               quest_givers = { "farmer_npc" },
               completion_npcs = { "farmer_npc" },
               repeatable = false,
               max_active_per_character = 1,
               objectives = {
                   quest.kill({ mobiles = { "{{mobileTemplateId}}" }, amount = 5 })
               },
               rewards = {
                   quest.gold(25)
               }
           })
           """;

    private static DirectoriesConfig CreateDirectoriesConfig(string rootPath)
        => new(
            rootPath,
            DirectoryType.Data,
            DirectoryType.Templates,
            DirectoryType.Scripts,
            DirectoryType.Save,
            DirectoryType.Logs,
            DirectoryType.Cache
        );
}
