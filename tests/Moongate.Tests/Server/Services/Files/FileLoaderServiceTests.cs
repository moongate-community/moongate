using DryIoc;
using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.Server.Services.Files;
using Moongate.Server.Services.Scripting;
using Moongate.Tests.Server.Support;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Interfaces.Templates;
using Moongate.UO.Data.Services.Templates;

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
        container.RegisterInstance(directoriesConfig);
        container.Register<IQuestDefinitionService, QuestDefinitionService>(Reuse.Singleton);
        container.Register<IQuestTemplateService, QuestTemplateService>(Reuse.Singleton);

        var service = new FileLoaderService(container, directoriesConfig);

        await service.LoadSingleAsync(questPath);

        var questTemplates = container.Resolve<IQuestTemplateService>();

        Assert.That(questTemplates.TryGet("new_haven.rat_hunt", out var definition), Is.True);
        Assert.That(definition, Is.Not.Null);
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
}
