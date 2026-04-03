using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Scripting;
using Moongate.Server.Interfaces.Services.Files;
using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.UO.Data.Interfaces.Templates;
using Moongate.UO.Data.Templates.Quests;
using MoonSharp.Interpreter;
using Serilog;

namespace Moongate.Server.FileLoaders;

/// <summary>
/// Loads Lua-authored quest templates from <c>scripts/quests</c>.
/// </summary>
[RegisterFileLoader(15)]
public sealed class QuestTemplateLoader : IFileLoader
{
    private readonly ILogger _logger = Log.ForContext<QuestTemplateLoader>();
    private readonly DirectoriesConfig _directoriesConfig;
    private readonly IQuestDefinitionService _questDefinitionService;
    private readonly IQuestTemplateService _questTemplateService;
    private readonly Dictionary<string, string> _scriptFileContents = new(StringComparer.OrdinalIgnoreCase);

    public QuestTemplateLoader(
        DirectoriesConfig directoriesConfig,
        IQuestDefinitionService questDefinitionService,
        IQuestTemplateService questTemplateService
    )
    {
        _directoriesConfig = directoriesConfig;
        _questDefinitionService = questDefinitionService;
        _questTemplateService = questTemplateService;
    }

    public Task LoadAsync()
    {
        var scriptsRootDirectory = Path.Combine(_directoriesConfig[DirectoryType.Scripts], "quests");

        if (!Directory.Exists(scriptsRootDirectory))
        {
            _logger.Warning("Quest scripts directory not found: {Directory}", scriptsRootDirectory);

            return Task.CompletedTask;
        }

        var scriptFiles = Directory.GetFiles(scriptsRootDirectory, "*.lua", SearchOption.AllDirectories);

        if (scriptFiles.Length == 0)
        {
            _logger.Warning("No quest scripts found in {Directory}", scriptsRootDirectory);
            _questDefinitionService.Clear();
            _questTemplateService.Clear();

            return Task.CompletedTask;
        }

        _scriptFileContents.Clear();

        foreach (var scriptFile in scriptFiles)
        {
            _scriptFileContents[NormalizePath(scriptFile)] = File.ReadAllText(scriptFile);
        }

        var templates = RebuildTemplatesFromCache();
        _questTemplateService.Clear();
        _questTemplateService.UpsertRange(templates);

        _logger.Information(
            "Loaded {TemplateCount} quest templates from {FileCount} files",
            templates.Count,
            scriptFiles.Length
        );

        return Task.CompletedTask;
    }

    public Task LoadSingleAsync(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var normalizedPath = NormalizePath(filePath);
        var fileExists = File.Exists(normalizedPath);

        if (fileExists)
        {
            _scriptFileContents[normalizedPath] = File.ReadAllText(normalizedPath);
        }
        else if (!_scriptFileContents.Remove(normalizedPath))
        {
            return Task.CompletedTask;
        }

        var templates = RebuildTemplatesFromCache();
        _questTemplateService.Clear();
        _questTemplateService.UpsertRange(templates);

        if (fileExists)
        {
            _logger.Information("Reloaded quest script file {ScriptFile}", normalizedPath);
        }
        else
        {
            _logger.Information("Removed quest script file {ScriptFile}", normalizedPath);
        }

        return Task.CompletedTask;
    }

    public QuestTemplateLoaderState CaptureState()
        => new(
            new Dictionary<string, string>(_scriptFileContents, StringComparer.OrdinalIgnoreCase),
            _questDefinitionService.GetAll(),
            _questTemplateService.GetAll()
        );

    public void RestoreState(QuestTemplateLoaderState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        _scriptFileContents.Clear();

        foreach (var (scriptFile, content) in state.ScriptFileContents)
        {
            _scriptFileContents[scriptFile] = content;
        }

        _questDefinitionService.ReplaceAll(state.QuestDefinitions);
        _questTemplateService.ReplaceAll(state.QuestTemplates);
    }

    private Script CreateQuestDslScript(string relativeScriptPath)
    {
        var script = new Script();
        var quest = new Table(script);

        quest["define"] = DynValue.NewCallback(
            (_, args) =>
            {
                var definition = RequireTableArgument(args, "quest.define definition table is required");
                _questDefinitionService.Register(definition, relativeScriptPath);

                return DynValue.NewTable(definition);
            }
        );

        quest["kill"] = DynValue.NewCallback(
            (_, args) => DynValue.NewTable(WithType(script, RequireTableArgument(args, "quest.kill definition table is required"), "kill"))
        );
        quest["collect"] = DynValue.NewCallback(
            (_, args) => DynValue.NewTable(WithType(script, RequireTableArgument(args, "quest.collect definition table is required"), "collect"))
        );
        quest["deliver"] = DynValue.NewCallback(
            (_, args) => DynValue.NewTable(WithType(script, RequireTableArgument(args, "quest.deliver definition table is required"), "deliver"))
        );
        quest["gold"] = DynValue.NewCallback(
            (_, args) => DynValue.NewTable(CreateGoldReward(script, args))
        );
        quest["item"] = DynValue.NewCallback(
            (_, args) => DynValue.NewTable(CreateItemReward(script, args))
        );

        script.Globals["quest"] = quest;

        return script;
    }

    private static Table CreateGoldReward(Script script, CallbackArguments args)
    {
        if (args.Count == 0 || args[0].Type != DataType.Number)
        {
            throw new ScriptRuntimeException("quest.gold amount is required");
        }

        return new Table(script)
        {
            ["type"] = "gold",
            ["amount"] = (int)args[0].Number
        };
    }

    private static Table CreateItemReward(Script script, CallbackArguments args)
    {
        if (args.Count < 2 || args[0].Type != DataType.String || string.IsNullOrWhiteSpace(args[0].String))
        {
            throw new ScriptRuntimeException("quest.item item_template_id is required");
        }

        if (args[1].Type != DataType.Number)
        {
            throw new ScriptRuntimeException("quest.item amount is required");
        }

        return new Table(script)
        {
            ["type"] = "item",
            ["item_template_id"] = args[0].String.Trim(),
            ["amount"] = (int)args[1].Number
        };
    }

    private static Table RequireTableArgument(CallbackArguments args, string message)
    {
        if (args.Count == 0 || args[0].Type != DataType.Table || args[0].Table is null)
        {
            throw new ScriptRuntimeException(message);
        }

        return args[0].Table;
    }

    private List<Moongate.UO.Data.Templates.Quests.QuestTemplateDefinition> RebuildTemplatesFromCache()
    {
        _questDefinitionService.Clear();

        foreach (var (scriptFile, content) in _scriptFileContents.OrderBy(static entry => entry.Key, StringComparer.OrdinalIgnoreCase))
        {
            var relativeScriptPath = GetRelativeScriptPath(scriptFile);

            try
            {
                var script = CreateQuestDslScript(relativeScriptPath);
                _ = script.DoString(content, null, relativeScriptPath);
            }
            catch (InterpreterException ex)
            {
                _logger.Error(ex, "Failed to load quest script {ScriptFile}", relativeScriptPath);

                throw new InvalidOperationException($"Failed to load quest script '{relativeScriptPath}'.", ex);
            }
        }

        return _questDefinitionService.GetAll().Select(static definition => definition.Compile()).ToList();
    }

    private string GetRelativeScriptPath(string scriptFile)
    {
        var relativePath = Path.GetRelativePath(_directoriesConfig[DirectoryType.Scripts], scriptFile)
                               .Replace(Path.DirectorySeparatorChar, '/')
                               .Replace(Path.AltDirectorySeparatorChar, '/');

        return $"scripts/{relativePath}";
    }

    private static Table WithType(Script script, Table definition, string type)
    {
        var typed = new Table(script);

        foreach (var pair in definition.Pairs)
        {
            typed.Set(pair.Key, pair.Value);
        }

        typed["type"] = type;

        return typed;
    }

    private static string NormalizePath(string path)
        => Path.GetFullPath(path)
               .Replace(Path.DirectorySeparatorChar, '/')
               .Replace(Path.AltDirectorySeparatorChar, '/');

    public sealed record QuestTemplateLoaderState(
        IReadOnlyDictionary<string, string> ScriptFileContents,
        IReadOnlyList<QuestLuaDefinition> QuestDefinitions,
        IReadOnlyList<QuestTemplateDefinition> QuestTemplates
    );
}
