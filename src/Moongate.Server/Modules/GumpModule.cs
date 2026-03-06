using System.Globalization;
using System.Text.RegularExpressions;
using Moongate.Network.Packets.Outgoing.UI;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Scripting.Descriptors;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Modules.Builders;
using MoonSharp.Interpreter;

namespace Moongate.Server.Modules;

[ScriptModule("gump", "Provides fluent gump layout building APIs.")]

/// <summary>
/// Exposes gump-building helpers to Lua scripts.
/// </summary>
public sealed class GumpModule
{
    private static readonly Regex ContextPlaceholderRegex = new(@"\$ctx\.([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);
    private static bool _isBuilderTypeRegistered;
    private readonly IOutgoingPacketQueue? _outgoingPacketQueue;
    private readonly IGameNetworkSessionService? _gameNetworkSessionService;
    private readonly IGumpScriptDispatcherService? _gumpScriptDispatcherService;

    public GumpModule(
        IOutgoingPacketQueue? outgoingPacketQueue = null,
        IGameNetworkSessionService? gameNetworkSessionService = null,
        IGumpScriptDispatcherService? gumpScriptDispatcherService = null
    )
    {
        _outgoingPacketQueue = outgoingPacketQueue;
        _gameNetworkSessionService = gameNetworkSessionService;
        _gumpScriptDispatcherService = gumpScriptDispatcherService;
    }

    [ScriptFunction("create", "Creates a new gump builder instance.")]
    public LuaGumpBuilder Create()
    {
        if (!_isBuilderTypeRegistered)
        {
            var type = typeof(LuaGumpBuilder);
            UserData.RegisterType(type, new GenericUserDataDescriptor(type));
            _isBuilderTypeRegistered = true;
        }

        return new();
    }

    [ScriptFunction("on", "Registers a Lua callback for a gump button response.")]
    public bool On(uint gumpId, uint buttonId, Closure handler)
    {
        if (gumpId == 0 || buttonId == 0 || handler is null || _gumpScriptDispatcherService is null)
        {
            return false;
        }

        _gumpScriptDispatcherService.RegisterHandler(gumpId, buttonId, handler);

        return true;
    }

    [ScriptFunction("send", "Sends a compressed gump to a target session.")]
    public bool Send(
        long sessionId,
        LuaGumpBuilder builder,
        uint senderSerial = 0,
        uint gumpId = 1,
        uint x = 50,
        uint y = 50
    )
    {
        if (sessionId <= 0 || builder is null || _outgoingPacketQueue is null || _gameNetworkSessionService is null)
        {
            return false;
        }

        if (!_gameNetworkSessionService.TryGet(sessionId, out _))
        {
            return false;
        }

        var packet = new CompressedGumpPacket
        {
            SenderSerial = senderSerial,
            GumpId = gumpId,
            X = x,
            Y = y,
            Layout = builder.BuildLayout()
        };

        packet.TextLines.AddRange(builder.BuildTexts());
        _outgoingPacketQueue.Enqueue(sessionId, packet);

        return true;
    }

    [ScriptFunction("send_layout", "Sends a gump from a Lua table layout definition.")]
    public bool SendLayout(
        long sessionId,
        Table layoutDefinition,
        uint senderSerial = 0,
        uint gumpId = 1,
        uint x = 50,
        uint y = 50,
        Table? context = null
    )
    {
        if (layoutDefinition is null)
        {
            return false;
        }

        var builder = Create();

        if (!TryBuildFromLayout(layoutDefinition, builder, gumpId, context))
        {
            return false;
        }

        return Send(sessionId, builder, senderSerial, gumpId, x, y);
    }

    private static bool GetBool(Table table, string key, bool defaultValue)
    {
        var dyn = table.Get(key);

        if (dyn.Type == DataType.Boolean)
        {
            return dyn.Boolean;
        }

        if (dyn.Type == DataType.Number)
        {
            return Math.Abs(dyn.Number) > double.Epsilon;
        }

        return defaultValue;
    }

    private static int GetInt(Table table, string key, int defaultValue)
        => TryGetInt(table, key, out var value) ? value : defaultValue;

    private static string GetString(Table table, string key, string defaultValue)
        => TryGetString(table, key, out var value) ? value : defaultValue;

    private static string ResolveContextPlaceholders(string value, Table? context)
    {
        if (string.IsNullOrEmpty(value) || context is null)
        {
            return value;
        }

        return ContextPlaceholderRegex.Replace(
            value,
            match =>
            {
                var key = match.Groups[1].Value;
                var dyn = context.Get(key);

                return dyn.Type switch
                {
                    DataType.String => dyn.String ?? string.Empty,
                    DataType.Number => dyn.Number.ToString(
                        "0.###############################",
                        CultureInfo.InvariantCulture
                    ),
                    DataType.Boolean => dyn.Boolean ? "true" : "false",
                    _                => match.Value
                };
            }
        );
    }

    private bool TryBuildEntry(Table entry, Table? handlersTable, LuaGumpBuilder builder, uint gumpId, Table? context)
    {
        if (!TryGetString(entry, "type", out var type))
        {
            return false;
        }

        switch (type.Trim().ToLowerInvariant())
        {
            case "page":
                {
                    var page = GetInt(entry, "index", 0);
                    builder.Page(page);

                    return true;
                }
            case "group":
                {
                    if (!TryGetInt(entry, "id", out var groupId))
                    {
                        return false;
                    }

                    builder.Group(groupId);

                    return true;
                }
            case "background":
                {
                    if (!TryGetInt(entry, "x", out var x) ||
                        !TryGetInt(entry, "y", out var y) ||
                        !TryGetInt(entry, "gump_id", out var backgroundGumpId) ||
                        !TryGetInt(entry, "width", out var width) ||
                        !TryGetInt(entry, "height", out var height))
                    {
                        return false;
                    }

                    builder.ResizePic(x, y, backgroundGumpId, width, height);

                    return true;
                }
            case "alpha_region":
                {
                    if (!TryGetInt(entry, "x", out var x) ||
                        !TryGetInt(entry, "y", out var y) ||
                        !TryGetInt(entry, "width", out var width) ||
                        !TryGetInt(entry, "height", out var height))
                    {
                        return false;
                    }

                    builder.AlphaRegion(x, y, width, height);

                    return true;
                }
            case "image":
                {
                    if (!TryGetInt(entry, "x", out var x) ||
                        !TryGetInt(entry, "y", out var y) ||
                        !TryGetInt(entry, "gump_id", out var imageGumpId))
                    {
                        return false;
                    }

                    builder.Image(x, y, imageGumpId, GetInt(entry, "hue", 0));

                    return true;
                }
            case "image_tiled":
                {
                    if (!TryGetInt(entry, "x", out var x) ||
                        !TryGetInt(entry, "y", out var y) ||
                        !TryGetInt(entry, "width", out var width) ||
                        !TryGetInt(entry, "height", out var height) ||
                        !TryGetInt(entry, "gump_id", out var imageTiledGumpId))
                    {
                        return false;
                    }

                    builder.ImageTiled(x, y, width, height, imageTiledGumpId);

                    return true;
                }
            case "item":
                {
                    if (!TryGetInt(entry, "x", out var x) ||
                        !TryGetInt(entry, "y", out var y) ||
                        !TryGetInt(entry, "item_id", out var itemId))
                    {
                        return false;
                    }

                    builder.Item(x, y, itemId, GetInt(entry, "hue", 0));

                    return true;
                }
            case "label":
                {
                    if (!TryGetInt(entry, "x", out var x) ||
                        !TryGetInt(entry, "y", out var y) ||
                        !TryGetInt(entry, "hue", out var hue) ||
                        !TryGetString(entry, "text", out var text))
                    {
                        return false;
                    }

                    builder.Text(x, y, hue, ResolveContextPlaceholders(text, context));

                    return true;
                }
            case "label_cropped":
                {
                    if (!TryGetInt(entry, "x", out var x) ||
                        !TryGetInt(entry, "y", out var y) ||
                        !TryGetInt(entry, "width", out var width) ||
                        !TryGetInt(entry, "height", out var height) ||
                        !TryGetInt(entry, "hue", out var hue) ||
                        !TryGetString(entry, "text", out var text))
                    {
                        return false;
                    }

                    builder.LabelCropped(x, y, width, height, hue, ResolveContextPlaceholders(text, context));

                    return true;
                }
            case "html":
                {
                    if (!TryGetInt(entry, "x", out var x) ||
                        !TryGetInt(entry, "y", out var y) ||
                        !TryGetInt(entry, "width", out var width) ||
                        !TryGetInt(entry, "height", out var height) ||
                        !TryGetString(entry, "text", out var text))
                    {
                        return false;
                    }

                    builder.Html(
                        x,
                        y,
                        width,
                        height,
                        ResolveContextPlaceholders(text, context),
                        GetBool(entry, "background", true),
                        GetBool(entry, "scrollbar", false)
                    );

                    return true;
                }
            case "checkbox":
                {
                    if (!TryGetInt(entry, "x", out var x) ||
                        !TryGetInt(entry, "y", out var y) ||
                        !TryGetInt(entry, "inactive_id", out var inactiveId) ||
                        !TryGetInt(entry, "active_id", out var activeId) ||
                        !TryGetInt(entry, "switch_id", out var switchId))
                    {
                        return false;
                    }

                    builder.CheckBox(x, y, inactiveId, activeId, switchId, GetBool(entry, "initial_state", false));

                    return true;
                }
            case "radio":
                {
                    if (!TryGetInt(entry, "x", out var x) ||
                        !TryGetInt(entry, "y", out var y) ||
                        !TryGetInt(entry, "inactive_id", out var inactiveId) ||
                        !TryGetInt(entry, "active_id", out var activeId) ||
                        !TryGetInt(entry, "switch_id", out var switchId))
                    {
                        return false;
                    }

                    builder.Radio(x, y, inactiveId, activeId, switchId, GetBool(entry, "initial_state", false));

                    return true;
                }
            case "text_entry":
                {
                    if (!TryGetInt(entry, "x", out var x) ||
                        !TryGetInt(entry, "y", out var y) ||
                        !TryGetInt(entry, "width", out var width) ||
                        !TryGetInt(entry, "height", out var height) ||
                        !TryGetInt(entry, "hue", out var hue) ||
                        !TryGetInt(entry, "entry_id", out var entryId))
                    {
                        return false;
                    }

                    builder.TextEntry(
                        x,
                        y,
                        width,
                        height,
                        hue,
                        entryId,
                        ResolveContextPlaceholders(GetString(entry, "text", string.Empty), context)
                    );

                    return true;
                }
            case "text_entry_limited":
                {
                    if (!TryGetInt(entry, "x", out var x) ||
                        !TryGetInt(entry, "y", out var y) ||
                        !TryGetInt(entry, "width", out var width) ||
                        !TryGetInt(entry, "height", out var height) ||
                        !TryGetInt(entry, "hue", out var hue) ||
                        !TryGetInt(entry, "entry_id", out var entryId))
                    {
                        return false;
                    }

                    builder.TextEntryLimited(
                        x,
                        y,
                        width,
                        height,
                        hue,
                        entryId,
                        ResolveContextPlaceholders(GetString(entry, "text", string.Empty), context),
                        GetInt(entry, "size", 0)
                    );

                    return true;
                }
            case "tooltip":
                {
                    if (!TryGetInt(entry, "number", out var number))
                    {
                        return false;
                    }

                    builder.ToolTip(number);

                    return true;
                }
            case "button":
                {
                    if (!TryGetInt(entry, "id", out var buttonId) ||
                        !TryGetInt(entry, "x", out var x) ||
                        !TryGetInt(entry, "y", out var y) ||
                        !TryGetInt(entry, "normal_id", out var normalId) ||
                        !TryGetInt(entry, "pressed_id", out var pressedId))
                    {
                        return false;
                    }

                    builder.Button(x, y, normalId, pressedId, buttonId);

                    if (!TryGetString(entry, "onclick", out var onClickName) || string.IsNullOrWhiteSpace(onClickName))
                    {
                        return true;
                    }

                    if (handlersTable is null || _gumpScriptDispatcherService is null)
                    {
                        return false;
                    }

                    var handlerValue = handlersTable.Get(onClickName);

                    if (handlerValue.Type != DataType.Function || handlerValue.Function is null)
                    {
                        return false;
                    }

                    _gumpScriptDispatcherService.RegisterHandler(gumpId, (uint)buttonId, handlerValue.Function);

                    return true;
                }
            case "button_page":
                {
                    if (!TryGetInt(entry, "x", out var x) ||
                        !TryGetInt(entry, "y", out var y) ||
                        !TryGetInt(entry, "normal_id", out var normalId) ||
                        !TryGetInt(entry, "pressed_id", out var pressedId) ||
                        !TryGetInt(entry, "page_id", out var pageId))
                    {
                        return false;
                    }

                    builder.ButtonPage(x, y, normalId, pressedId, pageId);

                    return true;
                }
            default:
                return false;
        }
    }

    private bool TryBuildFromLayout(Table layoutDefinition, LuaGumpBuilder builder, uint gumpId, Table? context)
    {
        if (!TryGetTable(layoutDefinition, "ui", out var uiTable))
        {
            return false;
        }

        TryGetTable(layoutDefinition, "handlers", out var handlersTable);

        for (var index = 1;; index++)
        {
            var entryValue = uiTable.Get(index);

            if (entryValue.IsNil())
            {
                break;
            }

            if (entryValue.Type != DataType.Table)
            {
                return false;
            }

            if (!TryBuildEntry(entryValue.Table, handlersTable, builder, gumpId, context))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryGetInt(Table table, string key, out int value)
    {
        value = 0;
        var dyn = table.Get(key);

        if (dyn.Type != DataType.Number)
        {
            return false;
        }

        value = (int)dyn.Number;

        return true;
    }

    private static bool TryGetString(Table table, string key, out string value)
    {
        value = string.Empty;
        var dyn = table.Get(key);

        if (dyn.Type != DataType.String || dyn.String is null)
        {
            return false;
        }

        value = dyn.String;

        return true;
    }

    private static bool TryGetTable(Table table, string key, out Table value)
    {
        value = null!;
        var dyn = table.Get(key);

        if (dyn.Type != DataType.Table || dyn.Table is null)
        {
            return false;
        }

        value = dyn.Table;

        return true;
    }
}
