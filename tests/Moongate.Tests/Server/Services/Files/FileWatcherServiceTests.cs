using System.Reflection;
using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Scripting.Interfaces;
using Moongate.Server.Data.Config;
using Moongate.Server.Interfaces.Services.EvenLoop;
using Moongate.Server.Interfaces.Services.Files;
using Moongate.Server.Services.Files;
using Moongate.Tests.TestSupport;

namespace Moongate.Tests.Server.Services.Files;

public sealed class FileWatcherServiceTests
{
    [Test]
    public void ProcessChangeOnGameLoop_WhenQuestLuaFileChanges_ShouldReloadQuestTemplates()
    {
        using var tempDirectory = new TempDirectory();
        var directoriesConfig = CreateDirectoriesConfig(tempDirectory.Path);
        var questPath = WriteFile(directoriesConfig, "scripts/quests/new_haven/rat_hunt.lua");
        var fileLoaderService = new FileLoaderServiceSpy();
        var scriptEngineService = new ScriptEngineServiceSpy();
        var service = CreateService(directoriesConfig, fileLoaderService, scriptEngineService);

        InvokeProcessChangeOnGameLoop(service, questPath);

        Assert.Multiple(
            () =>
            {
                Assert.That(
                    fileLoaderService.QuestReloadRequests,
                    Is.EqualTo(new List<(string? RemovedFilePath, string? LoadedFilePath)> { ((string?)null, questPath) })
                );
                Assert.That(scriptEngineService.InvalidatedScripts, Is.Empty);
            }
        );
    }

    [Test]
    public void ProcessChangeOnGameLoop_WhenQuestLuaFileIsDeleted_ShouldEvictQuestTemplates()
    {
        using var tempDirectory = new TempDirectory();
        var directoriesConfig = CreateDirectoriesConfig(tempDirectory.Path);
        var questPath = Path.Combine(directoriesConfig[DirectoryType.Scripts], "quests", "new_haven", "rat_hunt.lua");
        var fileLoaderService = new FileLoaderServiceSpy();
        var scriptEngineService = new ScriptEngineServiceSpy();
        var service = CreateService(directoriesConfig, fileLoaderService, scriptEngineService);

        InvokeProcessChangeOnGameLoop(service, questPath);

        Assert.Multiple(
            () =>
            {
                Assert.That(
                    fileLoaderService.QuestReloadRequests,
                    Is.EqualTo(new List<(string? RemovedFilePath, string? LoadedFilePath)> { (questPath, (string?)null) })
                );
                Assert.That(scriptEngineService.InvalidatedScripts, Is.Empty);
            }
        );
    }

    [Test]
    public async Task StartAsync_WhenQuestLuaFileIsDeleted_ShouldDispatchQuestRemovalReload()
    {
        using var tempDirectory = new TempDirectory();
        var directoriesConfig = CreateDirectoriesConfig(tempDirectory.Path);
        var questPath = WriteFile(directoriesConfig, "scripts/quests/new_haven/rat_hunt.lua");
        var fileLoaderService = new FileLoaderServiceSpy();
        var scriptEngineService = new ScriptEngineServiceSpy();
        var service = CreateService(directoriesConfig, fileLoaderService, scriptEngineService);

        await service.StartAsync();

        try
        {
            File.Delete(questPath);

            Assert.That(
                SpinWait.SpinUntil(() => fileLoaderService.QuestReloadRequests.Count == 1, TimeSpan.FromSeconds(5)),
                Is.True,
                "Timed out waiting for quest delete watcher reload."
            );

            Assert.That(
                fileLoaderService.QuestReloadRequests,
                Is.EqualTo(new List<(string? RemovedFilePath, string? LoadedFilePath)> { (questPath, (string?)null) })
            );
            Assert.That(scriptEngineService.InvalidatedScripts, Is.Empty);
        }
        finally
        {
            await service.StopAsync();
        }
    }

    [Test]
    public void ProcessRenameOnGameLoop_WhenQuestLuaFileChanges_ShouldReloadQuestTemplatesAtomically()
    {
        using var tempDirectory = new TempDirectory();
        var directoriesConfig = CreateDirectoriesConfig(tempDirectory.Path);
        var oldQuestPath = WriteFile(directoriesConfig, "scripts/quests/new_haven/rat_hunt.lua");
        var newQuestPath = WriteFile(directoriesConfig, "scripts/quests/new_haven/rat_hunt_renamed.lua");
        var fileLoaderService = new FileLoaderServiceSpy();
        var scriptEngineService = new ScriptEngineServiceSpy();
        var service = CreateService(directoriesConfig, fileLoaderService, scriptEngineService);

        InvokeProcessRenameOnGameLoop(service, oldQuestPath, newQuestPath);

        Assert.Multiple(
            () =>
            {
                Assert.That(
                    fileLoaderService.QuestReloadRequests,
                    Is.EqualTo(new List<(string? RemovedFilePath, string? LoadedFilePath)> { (oldQuestPath, newQuestPath) })
                );
                Assert.That(scriptEngineService.InvalidatedScripts, Is.Empty);
            }
        );
    }

    [Test]
    public void ProcessChangeOnGameLoop_WhenGenericLuaFileChanges_ShouldInvalidateScriptCache()
    {
        using var tempDirectory = new TempDirectory();
        var directoriesConfig = CreateDirectoriesConfig(tempDirectory.Path);
        var scriptPath = WriteFile(directoriesConfig, "scripts/items/door.lua");
        var fileLoaderService = new FileLoaderServiceSpy();
        var scriptEngineService = new ScriptEngineServiceSpy();
        var service = CreateService(directoriesConfig, fileLoaderService, scriptEngineService);

        InvokeProcessChangeOnGameLoop(service, scriptPath);

        Assert.Multiple(
            () =>
            {
                Assert.That(fileLoaderService.LoadedFilePaths, Is.Empty);
                Assert.That(scriptEngineService.InvalidatedScripts, Is.EqualTo([scriptPath]));
            }
        );
    }

    private static FileWatcherService CreateService(
        DirectoriesConfig directoriesConfig,
        IFileLoaderService fileLoaderService,
        IScriptEngineService scriptEngineService
    )
        => new(
            directoriesConfig,
            new MoongateConfig(),
            new ImmediateBackgroundJobService(),
            scriptEngineService,
            fileLoaderService
        );

    private static void InvokeProcessChangeOnGameLoop(FileWatcherService service, string filePath)
    {
        var method = typeof(FileWatcherService).GetMethod(
            "ProcessChangeOnGameLoop",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        Assert.That(method, Is.Not.Null);
        method!.Invoke(service, [filePath]);
    }

    private static void InvokeProcessRenameOnGameLoop(FileWatcherService service, string oldFilePath, string newFilePath)
    {
        var method = typeof(FileWatcherService).GetMethod(
            "ProcessRenameOnGameLoop",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        Assert.That(method, Is.Not.Null);
        method!.Invoke(service, [oldFilePath, newFilePath]);
    }

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

    private static string WriteFile(DirectoriesConfig directoriesConfig, string relativePath)
    {
        var filePath = Path.Combine(directoriesConfig.Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, "-- test file");

        return filePath;
    }

    private sealed class FileLoaderServiceSpy : IFileLoaderService
    {
        public List<string> LoadedFilePaths { get; } = [];
        public List<(string? RemovedFilePath, string? LoadedFilePath)> QuestReloadRequests { get; } = [];

        public void AddFileLoader<T>() where T : IFileLoader
        {
        }

        public void AddFileLoader(Type loaderType)
        {
        }

        public Task ExecuteLoadersAsync()
            => Task.CompletedTask;

        public Task LoadSingleAsync(string filePath)
        {
            LoadedFilePaths.Add(filePath);

            return Task.CompletedTask;
        }

        public Task LoadSingleAsync<T>(string filePath) where T : IFileLoader
            => LoadSingleAsync(filePath);

        public Task LoadSingleAsync(Type loaderType, string filePath)
            => LoadSingleAsync(filePath);

        public Task ReloadQuestTemplateAsync(string? removedFilePath = null, string? loadedFilePath = null)
        {
            QuestReloadRequests.Add((removedFilePath, loadedFilePath));

            return Task.CompletedTask;
        }

        public Task StartAsync()
            => Task.CompletedTask;

        public Task StopAsync()
            => Task.CompletedTask;
    }

    private sealed class ImmediateBackgroundJobService : IBackgroundJobService
    {
        public void EnqueueBackground(Action job)
            => job();

        public void EnqueueBackground(Func<Task> job)
            => job().GetAwaiter().GetResult();

        public int ExecutePendingOnGameLoop(int maxActions = 100)
            => 0;

        public void PostToGameLoop(Action action)
            => action();

        public void RunBackgroundAndPostResult<TResult>(
            Func<TResult> backgroundJob,
            Action<TResult> onGameLoopResult,
            Action<Exception>? onGameLoopError = null
        )
        {
            var result = backgroundJob();
            onGameLoopResult(result);
        }

        public void RunBackgroundAndPostResultAsync<TResult>(
            Func<Task<TResult>> backgroundJob,
            Action<TResult> onGameLoopResult,
            Action<Exception>? onGameLoopError = null
        )
        {
            var result = backgroundJob().GetAwaiter().GetResult();
            onGameLoopResult(result);
        }

        public void Start(int? workerCount = null)
        {
        }

        public Task StopAsync()
            => Task.CompletedTask;
    }

    private sealed class ScriptEngineServiceSpy : IScriptEngineService
    {
        public List<string> InvalidatedScripts { get; } = [];

#pragma warning disable CS0067
        public event EventHandler<Moongate.Scripting.Data.Scripts.ScriptErrorInfo>? OnScriptError;
#pragma warning restore CS0067

        public void AddCallback(string name, Action<object[]> callback)
        {
        }

        public void AddConstant(string name, object value)
        {
        }

        public void AddInitScript(string script)
        {
        }

        public void AddManualModuleFunction(string moduleName, string functionName, Action<object[]> callback)
        {
        }

        public void AddManualModuleFunction<TInput, TOutput>(
            string moduleName,
            string functionName,
            Func<TInput?, TOutput> callback
        )
        {
        }

        public void AddScriptModule(Type type)
        {
        }

        public void CallFunction(string functionName, params object[] args)
        {
        }

        public void ClearScriptCache()
        {
        }

        public void InvalidateScript(string filePath)
            => InvalidatedScripts.Add(filePath);

        public void ExecuteCallback(string name, params object[] args)
        {
        }

        public void ExecuteEngineReady()
        {
        }

        public Moongate.Scripting.Data.Scripts.ScriptResult ExecuteFunction(string command)
            => new() { Success = true };

        public Task<Moongate.Scripting.Data.Scripts.ScriptResult> ExecuteFunctionAsync(string command)
            => Task.FromResult(new Moongate.Scripting.Data.Scripts.ScriptResult { Success = true });

        public void ExecuteFunctionFromBootstrap(string name)
        {
        }

        public void ExecuteScript(string script)
        {
        }

        public void ExecuteScriptFile(string scriptFile)
        {
        }

        public Moongate.Scripting.Data.Scripts.ScriptExecutionMetrics GetExecutionMetrics()
            => new();

        public void RegisterGlobal(string name, object value)
        {
        }

        public void RegisterGlobalFunction(string name, Delegate func)
        {
        }

        public Task StartAsync()
            => Task.CompletedTask;

        public Task StopAsync()
            => Task.CompletedTask;

        public string ToScriptEngineFunctionName(string name)
            => name;

        public bool UnregisterGlobal(string name)
            => true;
    }
}
