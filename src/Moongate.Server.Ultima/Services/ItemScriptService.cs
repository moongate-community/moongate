using Moongate.Scripting.Data.Config;
using Moongate.Scripting.Data.Scripts;
using Moongate.Scripting.Interfaces;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services.Internal;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Runs the item scripts: loads every <c>scripts/items/*.lua</c> at startup, after the script engine, and calls the
///     functions of the table an item's template names with <c>script_id</c>. Once stopped, before the engine, it calls
///     nothing.
/// </summary>
public sealed class ItemScriptService : IItemScriptService, IMoongateStartupService
{
    public const string ItemsDirectory = "items";

    private readonly ILogger _logger = Log.ForContext<ItemScriptService>();
    private readonly IScriptEngine _engine;
    private readonly IItemTemplateService _templates;
    private readonly IGameLoopService _loop;
    private readonly ScriptEngineOptions _options;

    private bool _running;

    public ItemScriptService(
        IScriptEngine engine,
        IItemTemplateService templates,
        IGameLoopService loop,
        ScriptEngineOptions options
    )
    {
        _engine = engine;
        _templates = templates;
        _loop = loop;
        _options = options;
    }

    public async Task StartAsync()
    {
        await ScriptDirectoryLoader.LoadAsync(_engine, _loop, _options.ScriptsDirectory, ItemsDirectory, _logger);
        _running = true;
    }

    public Task StopAsync()
    {
        _running = false;

        return Task.CompletedTask;
    }

    public bool HasScript(ItemEntity item)
    {
        return ScriptOf(item) is not null;
    }

    public bool Has(ItemEntity item, string function)
    {
        return _running && ScriptOf(item) is { } script && _engine.HasMember(script, function);
    }

    public ScriptResult Run(ItemEntity item, string function, params object?[] args)
    {
        if (!_running || ScriptOf(item) is not { } script)
        {
            return ScriptResult.Missing;
        }

        return _engine.CallMember($"{ItemsDirectory}/{script}.lua", script, function, [(long)item.Id.Value, .. args]);
    }

    public void Queue(ItemEntity item, string function, params object?[] args)
    {
        if (!_running || ScriptOf(item) is null)
        {
            return;
        }

        if (!_loop.TryPost(new LoopActionWorkItem(() => Run(item, function, args))))
        {
            _logger.Warning("Item {Serial}: {Function} was dropped, the game loop is full or stopping", item.Id, function);
        }
    }

    private string? ScriptOf(ItemEntity item)
    {
        return _templates.TryGet(item.TemplateId, out var template) && !string.IsNullOrEmpty(template.ScriptId)
            ? template.ScriptId
            : null;
    }
}
