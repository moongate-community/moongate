using System.Text.RegularExpressions;
using Moongate.Server.Abstractions.Data.Internal;
using Moongate.Server.Abstractions.Interfaces.Items;
using Moongate.Server.Abstractions.Types.Items;
using MoonSharp.Interpreter;
using Serilog;
using SquidStd.Core.Directories;

namespace Moongate.Scripting.Items;

/// <summary>
/// Runs item scripts: <c>&lt;scripts&gt;/items/&lt;ScriptId&gt;.lua</c> returns a table whose
/// <c>on_*</c> functions are the hooks. A smaller sibling of <c>LuaNpcBrainRuntime</c> — same lazy
/// load-by-id and the same guards, but no per-instance state and no bindings, because an item script
/// belongs to the id rather than to any one item.
/// </summary>
public sealed partial class LuaItemScriptRuntime : IItemScriptRuntime
{
    private const int MaximumScriptFileBytes = 256 * 1024;
    private const int InstructionBudget = 50_000;

    /// <summary>What every shipped template carries when it has no script at all.</summary>
    private const string NoScript = "none";

    private readonly ILogger _logger = Log.ForContext<LuaItemScriptRuntime>();
    private readonly Script _script;
    private readonly string _itemsDirectory;
    private readonly Dictionary<string, Table?> _definitions = new(StringComparer.Ordinal);
    private bool _seeded;

    public LuaItemScriptRuntime(Script script, DirectoriesConfig directoriesConfig)
    {
        _script = script;
        _itemsDirectory = Path.Combine(directoriesConfig.GetPath("scripts"), "items");
    }

    public bool HasHook(string scriptId, ItemScriptHookType hook)
        => TryGetDefinition(scriptId) is { } table && IsCallable(table.Get(GetHookName(hook)));

    public bool Invoke(ItemScriptHookType hook, ItemScriptContext context)
    {
        if (TryGetDefinition(context.Item.ScriptId) is not { } table)
        {
            return false;
        }

        var hookValue = table.Get(GetHookName(hook));

        if (!IsCallable(hookValue))
        {
            return false;
        }

        try
        {
            var coroutine = _script.CreateCoroutine(hookValue).Coroutine;
            coroutine.AutoYieldCounter = InstructionBudget;

            coroutine.Resume(DynValue.NewTable(BuildContextTable(hook, context)));

            if (coroutine.State == CoroutineState.ForceSuspended)
            {
                _logger.Warning(
                    "Item script '{ScriptId}' hook '{Hook}' exceeded its instruction budget",
                    context.Item.ScriptId,
                    GetHookName(hook)
                );

                return false;
            }

            return true;
        }
        catch (Exception exception)
        {
            // A content author's mistake must never reach the game loop.
            _logger.Error(
                exception,
                "Item script '{ScriptId}' hook '{Hook}' failed",
                context.Item.ScriptId,
                GetHookName(hook)
            );

            return false;
        }
    }

    private Table BuildContextTable(ItemScriptHookType hook, ItemScriptContext context)
    {
        var item = new Table(_script)
        {
            ["serial"] = (double)(uint)context.Item.Id,
            ["item_id"] = context.Item.ItemId,
            ["template_id"] = context.Item.TemplateId,
            ["script_id"] = context.Item.ScriptId,
            ["name"] = context.Item.Name,
            ["amount"] = context.Item.Amount,
            ["hue"] = (double)context.Item.Hue.Value,
            ["map_id"] = context.Item.MapId,
            ["x"] = context.Item.Position.X,
            ["y"] = context.Item.Position.Y,
            ["z"] = context.Item.Position.Z
        };

        var table = new Table(_script)
        {
            ["hook"] = GetHookName(hook),
            ["item"] = item,
            ["container_id"] = (double)(uint)context.ContainerId
        };

        if (context.Actor is { } actor)
        {
            table["actor"] = new Table(_script)
            {
                ["serial"] = (double)(uint)actor.Id,
                ["name"] = actor.Name,
                ["map_id"] = actor.MapId,
                ["x"] = actor.Position.X,
                ["y"] = actor.Position.Y,
                ["z"] = actor.Position.Z
            };
        }

        if (context.Layer is { } layer)
        {
            table["layer"] = layer.ToString();
        }

        return table;
    }

    private static string GetHookName(ItemScriptHookType hook)
        => hook switch
        {
            ItemScriptHookType.SingleClick => "on_single_click",
            ItemScriptHookType.DoubleClick => "on_double_click",
            ItemScriptHookType.Dropped     => "on_drop",
            ItemScriptHookType.Equipped    => "on_equip",
            ItemScriptHookType.Unequipped  => "on_unequip",
            _                              => $"unknown({(int)hook})"
        };

    private static bool IsCallable(DynValue value)
        => value.Type is DataType.Function or DataType.ClrFunction;

    [GeneratedRegex("^[a-z0-9_]+$")]
    private static partial Regex ValidScriptIdPattern();

    private Table? Load(string scriptId)
    {
        // The pattern is what keeps a ScriptId from walking out of the items directory.
        if (!ValidScriptIdPattern().IsMatch(scriptId))
        {
            _logger.Warning("Item script id '{ScriptId}' is not a valid name; ignored", scriptId);

            return null;
        }

        var path = Path.Combine(_itemsDirectory, scriptId + ".lua");

        if (!File.Exists(path))
        {
            _logger.Warning("Item script '{ScriptId}' was not found at {Path}", scriptId, path);

            return null;
        }

        try
        {
            var fileInfo = new FileInfo(path);

            if ((fileInfo.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                _logger.Warning("Item script '{ScriptId}' is a link; refused", scriptId);

                return null;
            }

            if (fileInfo.Length > MaximumScriptFileBytes)
            {
                _logger.Warning("Item script '{ScriptId}' exceeds 256 KiB; refused", scriptId);

                return null;
            }

            var chunk = _script.LoadFile(path, _script.Globals, path);
            var coroutine = _script.CreateCoroutine(chunk).Coroutine;
            coroutine.AutoYieldCounter = InstructionBudget;

            var result = coroutine.Resume();

            if (result.Type != DataType.Table)
            {
                _logger.Warning("Item script '{ScriptId}' did not return a table; ignored", scriptId);

                return null;
            }

            return result.Table;
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "Item script '{ScriptId}' failed to load", scriptId);

            return null;
        }
    }

    /// <summary>
    /// Loads a script the first time its id is asked for and remembers the outcome, misses included,
    /// so a template with a typo does not hit the disk on every click.
    /// </summary>
    private Table? TryGetDefinition(string? scriptId)
    {
        if (string.IsNullOrWhiteSpace(scriptId) ||
            string.Equals(scriptId, NoScript, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // Seeded lazily rather than in the constructor: construction must not touch the disk.
        if (!_seeded)
        {
            _seeded = true;
            ItemScriptAssetSeeder.SeedMissing(_itemsDirectory);
        }

        if (_definitions.TryGetValue(scriptId, out var cached))
        {
            return cached;
        }

        var definition = Load(scriptId);
        _definitions[scriptId] = definition;

        return definition;
    }
}
