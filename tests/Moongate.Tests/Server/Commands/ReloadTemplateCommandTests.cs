using Moongate.Server.Commands;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Interfaces.Services.Files;
using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Interfaces.FileLoaders;
using Moongate.UO.Data.Interfaces.Templates;
using Moongate.UO.Data.Templates.Items;
using Moongate.UO.Data.Templates.Mobiles;

namespace Moongate.Tests.Server.Commands;

public sealed class ReloadTemplateCommandTests
{
    private sealed class ReloadTemplateTestFileLoaderService : IFileLoaderService
    {
        public int ExecuteCalls { get; private set; }

        public bool ThrowOnExecute { get; set; }

        public void AddFileLoader<T>() where T : IFileLoader { }

        public void AddFileLoader(Type loaderType)
            => _ = loaderType;

        public Task ExecuteLoadersAsync()
        {
            ExecuteCalls++;

            if (ThrowOnExecute)
            {
                throw new InvalidOperationException("boom");
            }

            return Task.CompletedTask;
        }

        public Task StartAsync()
            => Task.CompletedTask;

        public Task StopAsync()
            => Task.CompletedTask;
    }

    private sealed class ReloadTemplateTestItemTemplateService : IItemTemplateService
    {
        public int Count { get; set; }

        public void Clear() { }

        public IReadOnlyList<ItemTemplateDefinition> GetAll()
            => [];

        public bool TryGet(string id, out ItemTemplateDefinition? definition)
        {
            _ = id;
            definition = null;

            return false;
        }

        public void Upsert(ItemTemplateDefinition definition)
            => _ = definition;

        public void UpsertRange(IEnumerable<ItemTemplateDefinition> templates)
            => _ = templates;
    }

    private sealed class ReloadTemplateTestMobileTemplateService : IMobileTemplateService
    {
        public int Count { get; set; }

        public void Clear() { }

        public IReadOnlyList<MobileTemplateDefinition> GetAll()
            => [];

        public bool TryGet(string id, out MobileTemplateDefinition? definition)
        {
            _ = id;
            definition = null;

            return false;
        }

        public void Upsert(MobileTemplateDefinition definition)
            => _ = definition;

        public void UpsertRange(IEnumerable<MobileTemplateDefinition> definitions)
            => _ = definitions;
    }

    [Test]
    public async Task ExecuteCommandAsync_WhenArgumentsProvided_ShouldPrintUsage()
    {
        var command = new ReloadTemplateCommand(
            new ReloadTemplateTestFileLoaderService(),
            new ReloadTemplateTestItemTemplateService(),
            new ReloadTemplateTestMobileTemplateService()
        );
        var output = new List<string>();
        var context = new CommandSystemContext(
            "reload_template now",
            ["now"],
            CommandSourceType.Console,
            0,
            (message, _) => output.Add(message)
        );

        await command.ExecuteCommandAsync(context);

        Assert.That(output[^1], Is.EqualTo("Usage: reload_template"));
    }

    [Test]
    public async Task ExecuteCommandAsync_WhenLoaderFails_ShouldPrintError()
    {
        var fileLoaderService = new ReloadTemplateTestFileLoaderService { ThrowOnExecute = true };
        var command = new ReloadTemplateCommand(
            fileLoaderService,
            new ReloadTemplateTestItemTemplateService(),
            new ReloadTemplateTestMobileTemplateService()
        );
        var output = new List<string>();
        var context = new CommandSystemContext(
            "reload_template",
            [],
            CommandSourceType.Console,
            0,
            (message, _) => output.Add(message)
        );

        await command.ExecuteCommandAsync(context);

        Assert.That(output[^1], Is.EqualTo("Failed to reload templates: boom"));
    }

    [Test]
    public async Task ExecuteCommandAsync_WhenSuccessful_ShouldExecuteLoadersAndPrintCounts()
    {
        var fileLoaderService = new ReloadTemplateTestFileLoaderService();
        var itemTemplateService = new ReloadTemplateTestItemTemplateService { Count = 123 };
        var mobileTemplateService = new ReloadTemplateTestMobileTemplateService { Count = 45 };
        var command = new ReloadTemplateCommand(fileLoaderService, itemTemplateService, mobileTemplateService);
        var output = new List<string>();
        var context = new CommandSystemContext(
            "reload_template",
            [],
            CommandSourceType.InGame,
            2,
            (message, _) => output.Add(message)
        );

        await command.ExecuteCommandAsync(context);

        Assert.Multiple(
            () =>
            {
                Assert.That(fileLoaderService.ExecuteCalls, Is.EqualTo(1));
                Assert.That(
                    output[^1],
                    Is.EqualTo("Templates reloaded successfully. ItemTemplates=123, MobileTemplates=45.")
                );
            }
        );
    }
}
