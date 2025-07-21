using System.Text.Json.Nodes;
using Jint.Native;
using Moongate.Core.Server.Attributes.Scripts;
using Moongate.Core.Server.Interfaces.Services;
using Moongate.Server.Utils;
using Moongate.Server.Wraps;
using Moongate.UO.Data.Interfaces.Actions;
using Moongate.UO.Data.Interfaces.Entities;
using Moongate.UO.Data.Interfaces.Services;
using Serilog;

namespace Moongate.Server.Modules;

[ScriptModule("items")]
public class ItemsModule
{
    private readonly ILogger _logger = Log.ForContext<ItemsModule>();

    private readonly IItemService _itemService;
    private readonly IScriptEngineService _scriptEngineService;

    public ItemsModule(IItemService itemService, IScriptEngineService scriptEngineService)
    {
        _itemService = itemService;
        _scriptEngineService = scriptEngineService;
    }


    [ScriptFunction("Add script to item")]
    public void AddScriptToItem(string itemId, JsValue classz)
    {
        JsInteropUtils.ImplementsInterface<IItemAction>(classz, _scriptEngineService);

        var itemScript = new ItemScriptWrap(_scriptEngineService, classz);

        _itemService.AddItemActionScript(itemId, itemScript);
    }
}
