using DryIoc;
using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.Data.Config;
using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.Server.Services.Files;
using Moongate.Server.Services.Scripting;
using Moongate.Tests.Server.Support;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Interfaces.Templates;
using Moongate.UO.Data.Services.Templates;
using Moongate.UO.Data.Templates.Mobiles;
using Moongate.UO.Data.Templates.Quests;

namespace Moongate.Tests.Server.Services.Files;

public class FileLoaderServiceTests
{
    public static List<string> ExecutionLog { get; } = [];

    [Test]
    public async Task AddFileLoader_WhenCalledTwiceForSameType_ShouldExecuteOnlyOnce()
    {
        using var container = new Container();
        var service = new FileLoaderService(container);

        service.AddFileLoader<FileLoaderServiceTestLoaderA>();
        service.AddFileLoader<FileLoaderServiceTestLoaderA>();

        await service.ExecuteLoadersAsync();

        Assert.That(ExecutionLog, Is.EqualTo(["A"]));
    }

    [Test]
    public void AddFileLoader_WhenTypeIsNotRegistered_ShouldRegisterItInContainer()
    {
        using var container = new Container();
        var service = new FileLoaderService(container);

        Assert.That(container.IsRegistered<FileLoaderServiceTestLoaderA>(), Is.False);

        service.AddFileLoader<FileLoaderServiceTestLoaderA>();

        Assert.That(container.IsRegistered<FileLoaderServiceTestLoaderA>(), Is.True);
    }

    [Test]
    public async Task ExecuteLoadersAsync_ShouldRunLoadersInInsertionOrder()
    {
        using var container = new Container();
        var service = new FileLoaderService(container);

        service.AddFileLoader<FileLoaderServiceTestLoaderA>();
        service.AddFileLoader<FileLoaderServiceTestLoaderB>();

        await service.ExecuteLoadersAsync();

        Assert.That(ExecutionLog, Is.EqualTo(["A", "B"]));
    }

    [Test]
    public async Task LoadSingleAsync_WhenQuestLuaPathIsUnderScriptsQuests_ShouldRouteToQuestTemplateLoader()
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
        var questsDirectory = Path.Combine(directoriesConfig[DirectoryType.Scripts], "quests");
        Directory.CreateDirectory(questsDirectory);

        var questPath = Path.Combine(questsDirectory, "rat_hunt.lua");
        await File.WriteAllTextAsync(
            questPath,
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
                    quest.kill({ mobiles = { "sewer_rat" }, amount = 10 })
                },
                rewards = {
                    quest.gold(150)
                }
            })
            """
        );

        using var container = new Container();
        var questDefinitionService = new QuestDefinitionService();
        var questTemplateService = new QuestTemplateService();
        var mobileTemplateService = new MobileTemplateService();
        container.RegisterInstance(directoriesConfig);
        container.RegisterInstance<IQuestDefinitionService>(questDefinitionService);
        container.RegisterInstance<IQuestTemplateService>(questTemplateService);
        container.RegisterInstance<IItemTemplateService>(new ItemTemplateService());
        mobileTemplateService.Upsert(CreateValidMobileTemplate("farmer_npc", "Farmer NPC"));
        mobileTemplateService.Upsert(CreateValidMobileTemplate("sewer_rat", "Sewer Rat"));
        container.RegisterInstance<IMobileTemplateService>(mobileTemplateService);
        container.RegisterInstance<IFactionTemplateService>(new FactionTemplateService());
        container.RegisterInstance<ISellProfileTemplateService>(new SellProfileTemplateService());
        container.RegisterInstance<ILootTemplateService>(new LootTemplateService());
        container.RegisterInstance<IBookTemplateService>(new BookTemplateService(directoriesConfig, new MoongateConfig()));

        var service = new FileLoaderService(container, directoriesConfig);

        await service.LoadSingleAsync(questPath);

        Assert.That(questTemplateService.TryGet("new_haven.rat_hunt", out var definition), Is.True);
        Assert.That(definition, Is.Not.Null);
        Assert.That(questDefinitionService.TryGet("new_haven.rat_hunt", out var questDefinition), Is.True);
        Assert.That(questDefinition, Is.Not.Null);
    }

    [Test]
    public async Task LoadSingleAsync_WhenQuestLuaValidationFails_ShouldPreserveExistingQuestState()
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
        var questsDirectory = Path.Combine(directoriesConfig[DirectoryType.Scripts], "quests");
        Directory.CreateDirectory(questsDirectory);

        var questPath = Path.Combine(questsDirectory, "rat_hunt.lua");
        await File.WriteAllTextAsync(
            questPath,
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
                    quest.kill({ mobiles = { "sewer_rat" }, amount = 10 })
                },
                rewards = {
                    quest.gold(150)
                }
            })
            """
        );

        using var container = new Container();
        var questDefinitionService = new QuestDefinitionService();
        var questTemplateService = new QuestTemplateService();
        var itemTemplateService = new ItemTemplateService();
        var mobileTemplateService = new MobileTemplateService();
        var factionTemplateService = new FactionTemplateService();
        var sellProfileTemplateService = new SellProfileTemplateService();
        var lootTemplateService = new LootTemplateService();
        var bookTemplateService = new BookTemplateService(directoriesConfig, new MoongateConfig());

        mobileTemplateService.Upsert(CreateValidMobileTemplate("farmer_npc", "Farmer NPC"));
        mobileTemplateService.Upsert(CreateValidMobileTemplate("sewer_rat", "Sewer Rat"));

        container.RegisterInstance(directoriesConfig);
        container.RegisterInstance<IQuestDefinitionService>(questDefinitionService);
        container.RegisterInstance<IQuestTemplateService>(questTemplateService);
        container.RegisterInstance<IItemTemplateService>(itemTemplateService);
        container.RegisterInstance<IMobileTemplateService>(mobileTemplateService);
        container.RegisterInstance<IFactionTemplateService>(factionTemplateService);
        container.RegisterInstance<ISellProfileTemplateService>(sellProfileTemplateService);
        container.RegisterInstance<ILootTemplateService>(lootTemplateService);
        container.RegisterInstance<IBookTemplateService>(bookTemplateService);

        var service = new FileLoaderService(container, directoriesConfig);

        await service.LoadSingleAsync(questPath);

        await File.WriteAllTextAsync(
            questPath,
            """
            quest.define({
                id = "new_haven.rat_hunt",
                name = "Rat Hunt Broken",
                category = "starter",
                description = "Cull the rat infestation near the mill.",
                quest_givers = { "missing_farmer_npc" },
                completion_npcs = { "farmer_npc" },
                repeatable = false,
                max_active_per_character = 1,
                objectives = {
                    quest.kill({ mobiles = { "missing_rat" }, amount = 10 })
                },
                rewards = {
                    quest.gold(150)
                }
            })
            """
        );

        Assert.That(
            async () => await service.LoadSingleAsync(questPath),
            Throws.TypeOf<InvalidOperationException>()
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(questTemplateService.TryGet("new_haven.rat_hunt", out var questTemplate), Is.True);
                Assert.That(questTemplate, Is.Not.Null);
                Assert.That(questTemplate!.Name, Is.EqualTo("Rat Hunt"));
                Assert.That(questTemplate.QuestGiverTemplateIds, Is.EqualTo(new[] { "farmer_npc" }));
                Assert.That(questTemplate.Objectives[0].MobileTemplateIds, Is.EqualTo(new[] { "sewer_rat" }));
                Assert.That(questDefinitionService.TryGet("new_haven.rat_hunt", out var questDefinition), Is.True);
                Assert.That(questDefinition, Is.Not.Null);
                Assert.That(questDefinition!.Name, Is.EqualTo("Rat Hunt"));
                Assert.That(questDefinition.QuestGiverTemplateIds, Is.EqualTo(new[] { "farmer_npc" }));
                Assert.That(questDefinition.Objectives[0].MobileTemplateIds, Is.EqualTo(new[] { "sewer_rat" }));
            }
        );
    }

    [Test]
    public async Task ReloadQuestTemplateAsync_WhenQuestLuaFileIsRenamed_ShouldEvictOldPathAndLoadNewPathAtomically()
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
        var questsDirectory = Path.Combine(directoriesConfig[DirectoryType.Scripts], "quests");
        Directory.CreateDirectory(questsDirectory);

        var oldQuestPath = Path.Combine(questsDirectory, "rat_hunt.lua");
        var newQuestPath = Path.Combine(questsDirectory, "rat_hunt_renamed.lua");

        await File.WriteAllTextAsync(
            oldQuestPath,
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
                    quest.kill({ mobiles = { "sewer_rat" }, amount = 10 })
                },
                rewards = {
                    quest.gold(150)
                }
            })
            """
        );

        using var container = new Container();
        var questDefinitionService = new QuestDefinitionService();
        var questTemplateService = new QuestTemplateService();
        var itemTemplateService = new ItemTemplateService();
        var mobileTemplateService = new MobileTemplateService();
        var factionTemplateService = new FactionTemplateService();
        var sellProfileTemplateService = new SellProfileTemplateService();
        var lootTemplateService = new LootTemplateService();
        var bookTemplateService = new BookTemplateService(directoriesConfig, new MoongateConfig());

        mobileTemplateService.Upsert(CreateValidMobileTemplate("farmer_npc", "Farmer NPC"));
        mobileTemplateService.Upsert(CreateValidMobileTemplate("sewer_rat", "Sewer Rat"));

        container.RegisterInstance(directoriesConfig);
        container.RegisterInstance<IQuestDefinitionService>(questDefinitionService);
        container.RegisterInstance<IQuestTemplateService>(questTemplateService);
        container.RegisterInstance<IItemTemplateService>(itemTemplateService);
        container.RegisterInstance<IMobileTemplateService>(mobileTemplateService);
        container.RegisterInstance<IFactionTemplateService>(factionTemplateService);
        container.RegisterInstance<ISellProfileTemplateService>(sellProfileTemplateService);
        container.RegisterInstance<ILootTemplateService>(lootTemplateService);
        container.RegisterInstance<IBookTemplateService>(bookTemplateService);

        var service = new FileLoaderService(container, directoriesConfig);

        await service.LoadSingleAsync(oldQuestPath);

        File.Delete(oldQuestPath);
        await File.WriteAllTextAsync(
            newQuestPath,
            """
            quest.define({
                id = "new_haven.rat_hunt",
                name = "Rat Hunt Renamed",
                category = "starter",
                description = "Cull the rat infestation near the mill.",
                quest_givers = { "farmer_npc" },
                completion_npcs = { "farmer_npc" },
                repeatable = false,
                max_active_per_character = 1,
                objectives = {
                    quest.kill({ mobiles = { "sewer_rat" }, amount = 10 })
                },
                rewards = {
                    quest.gold(150)
                }
            })
            """
        );

        await service.ReloadQuestTemplateAsync(oldQuestPath, newQuestPath);

        Assert.Multiple(
            () =>
            {
                Assert.That(questTemplateService.Count, Is.EqualTo(1));
                Assert.That(questTemplateService.TryGet("new_haven.rat_hunt", out var questTemplate), Is.True);
                Assert.That(questTemplate, Is.Not.Null);
                Assert.That(questTemplate!.Name, Is.EqualTo("Rat Hunt Renamed"));
                Assert.That(questDefinitionService.TryGet("new_haven.rat_hunt", out var questDefinition), Is.True);
                Assert.That(questDefinition, Is.Not.Null);
                Assert.That(questDefinition!.Name, Is.EqualTo("Rat Hunt Renamed"));
            }
        );
    }

    [Test]
    public async Task LoadSingleAsync_WhenFailedQuestRenameIsFollowedByUnrelatedReload_ShouldPreserveOldQuestState()
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
        var questsDirectory = Path.Combine(directoriesConfig[DirectoryType.Scripts], "quests");
        Directory.CreateDirectory(questsDirectory);

        var spiderQuestPath = Path.Combine(questsDirectory, "spider_cull.lua");
        var oldQuestPath = Path.Combine(questsDirectory, "rat_hunt.lua");
        var newQuestPath = Path.Combine(questsDirectory, "rat_hunt_renamed.lua");

        await File.WriteAllTextAsync(
            oldQuestPath,
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
                    quest.kill({ mobiles = { "sewer_rat" }, amount = 10 })
                },
                rewards = {
                    quest.gold(150)
                }
            })
            """
        );
        await File.WriteAllTextAsync(
            spiderQuestPath,
            """
            quest.define({
                id = "new_haven.spider_cull",
                name = "Spider Cull",
                category = "starter",
                description = "Cull the spider infestation near the mill.",
                quest_givers = { "farmer_npc" },
                completion_npcs = { "farmer_npc" },
                repeatable = false,
                max_active_per_character = 1,
                objectives = {
                    quest.kill({ mobiles = { "giant_spider" }, amount = 6 })
                },
                rewards = {
                    quest.gold(75)
                }
            })
            """
        );

        using var container = new Container();
        var questDefinitionService = new QuestDefinitionService();
        var questTemplateService = new QuestTemplateService();
        var mobileTemplateService = new MobileTemplateService();
        var itemTemplateService = new ItemTemplateService();
        var factionTemplateService = new FactionTemplateService();
        var sellProfileTemplateService = new SellProfileTemplateService();
        var lootTemplateService = new LootTemplateService();
        var bookTemplateService = new BookTemplateService(directoriesConfig, new MoongateConfig());

        mobileTemplateService.Upsert(CreateValidMobileTemplate("farmer_npc", "Farmer NPC"));
        mobileTemplateService.Upsert(CreateValidMobileTemplate("sewer_rat", "Sewer Rat"));
        mobileTemplateService.Upsert(CreateValidMobileTemplate("giant_spider", "Giant Spider"));

        container.RegisterInstance(directoriesConfig);
        container.RegisterInstance<IQuestDefinitionService>(questDefinitionService);
        container.RegisterInstance<IQuestTemplateService>(questTemplateService);
        container.RegisterInstance<IItemTemplateService>(itemTemplateService);
        container.RegisterInstance<IMobileTemplateService>(mobileTemplateService);
        container.RegisterInstance<IFactionTemplateService>(factionTemplateService);
        container.RegisterInstance<ISellProfileTemplateService>(sellProfileTemplateService);
        container.RegisterInstance<ILootTemplateService>(lootTemplateService);
        container.RegisterInstance<IBookTemplateService>(bookTemplateService);

        var service = new FileLoaderService(container, directoriesConfig);

        await service.LoadSingleAsync(oldQuestPath);
        await service.LoadSingleAsync(spiderQuestPath);

        File.Delete(oldQuestPath);
        await File.WriteAllTextAsync(
            newQuestPath,
            """
            quest.define({
                id = "new_haven.rat_hunt",
                name = "Rat Hunt Broken",
                category = "starter",
                description = "Cull the rat infestation near the mill.",
                quest_givers = { "missing_farmer_npc" },
                completion_npcs = { "farmer_npc" },
                repeatable = false,
                max_active_per_character = 1,
                objectives = {
                    quest.kill({ mobiles = { "missing_rat" }, amount = 10 })
                },
                rewards = {
                    quest.gold(150)
                }
            })
            """
        );

        Assert.That(
            async () => await service.ReloadQuestTemplateAsync(oldQuestPath, newQuestPath),
            Throws.TypeOf<InvalidOperationException>()
        );

        await File.WriteAllTextAsync(
            spiderQuestPath,
            """
            quest.define({
                id = "new_haven.spider_cull",
                name = "Spider Cull Updated",
                category = "starter",
                description = "Cull the spider infestation near the mill.",
                quest_givers = { "farmer_npc" },
                completion_npcs = { "farmer_npc" },
                repeatable = false,
                max_active_per_character = 1,
                objectives = {
                    quest.kill({ mobiles = { "giant_spider" }, amount = 8 })
                },
                rewards = {
                    quest.gold(90)
                }
            })
            """
        );
        await service.LoadSingleAsync(spiderQuestPath);

        Assert.Multiple(
            () =>
            {
                Assert.That(questTemplateService.Count, Is.EqualTo(2));
                Assert.That(questTemplateService.TryGet("new_haven.rat_hunt", out var failedQuestTemplate), Is.True);
                Assert.That(failedQuestTemplate, Is.Not.Null);
                Assert.That(failedQuestTemplate!.Name, Is.EqualTo("Rat Hunt"));
                Assert.That(questTemplateService.TryGet("new_haven.spider_cull", out var spiderQuestTemplate), Is.True);
                Assert.That(spiderQuestTemplate, Is.Not.Null);
                Assert.That(spiderQuestTemplate!.Name, Is.EqualTo("Spider Cull Updated"));
            }
        );

        await File.WriteAllTextAsync(
            newQuestPath,
            """
            quest.define({
                id = "new_haven.rat_hunt",
                name = "Rat Hunt Renamed",
                category = "starter",
                description = "Cull the rat infestation near the mill.",
                quest_givers = { "farmer_npc" },
                completion_npcs = { "farmer_npc" },
                repeatable = false,
                max_active_per_character = 1,
                objectives = {
                    quest.kill({ mobiles = { "sewer_rat" }, amount = 10 })
                },
                rewards = {
                    quest.gold(150)
                }
            })
            """
        );

        await service.LoadSingleAsync(newQuestPath);

        Assert.Multiple(
            () =>
            {
                Assert.That(questTemplateService.Count, Is.EqualTo(2));
                Assert.That(questTemplateService.TryGet("new_haven.rat_hunt", out var questTemplate), Is.True);
                Assert.That(questTemplate, Is.Not.Null);
                Assert.That(questTemplate!.Name, Is.EqualTo("Rat Hunt Renamed"));
                Assert.That(questTemplateService.TryGet("new_haven.spider_cull", out var spiderQuestTemplate), Is.True);
                Assert.That(spiderQuestTemplate, Is.Not.Null);
                Assert.That(spiderQuestTemplate!.Name, Is.EqualTo("Spider Cull Updated"));
                Assert.That(questDefinitionService.TryGet("new_haven.rat_hunt", out var questDefinition), Is.True);
                Assert.That(questDefinition, Is.Not.Null);
                Assert.That(questDefinition!.Name, Is.EqualTo("Rat Hunt Renamed"));
                Assert.That(questDefinition.ScriptPath, Is.EqualTo("scripts/quests/rat_hunt_renamed.lua"));
                Assert.That(questDefinitionService.TryGet("new_haven.spider_cull", out var spiderQuestDefinition), Is.True);
                Assert.That(spiderQuestDefinition, Is.Not.Null);
                Assert.That(spiderQuestDefinition!.Name, Is.EqualTo("Spider Cull Updated"));
                Assert.That(questDefinitionService.GetAll(), Has.Count.EqualTo(2));
            }
        );
    }

    [Test]
    public void ExecuteLoadersAsync_WhenLoaderThrows_ShouldPropagateException()
    {
        using var container = new Container();
        var service = new FileLoaderService(container);

        service.AddFileLoader<FileLoaderServiceTestLoaderThrows>();

        Assert.ThrowsAsync<InvalidOperationException>(async () => await service.ExecuteLoadersAsync());
    }

    [SetUp]
    public void SetUp()
        => ExecutionLog.Clear();

    private static MobileTemplateDefinition CreateValidMobileTemplate(string id, string name)
        => new()
        {
            Id = id,
            Name = name,
            Category = "test",
            Description = "test",
            Ai = new()
            {
                Brain = "ai_guard",
                FightMode = "closest",
                RangePerception = 1,
                RangeFight = 0
            },
            Variants =
            [
                new()
                {
                    Name = "default",
                    Appearance = new()
                    {
                        Body = 0x0190
                    }
                }
            ]
        };
}
