using Jint.Native;
using Moongate.Core.Server.Interfaces.Services;
using Moongate.UO.Data.Contexts;
using Moongate.UO.Data.Interfaces.Actions;

namespace Moongate.Server.Wraps;

public class ItemScriptWrap : BaseWrap, IItemAction
{
    public ItemScriptWrap(IScriptEngineService scriptEngineService, JsValue jsValue) : base(scriptEngineService, jsValue) { }

    public void OnUseItem(ItemUseContext context)
    {
        Call(nameof(OnUseItem), context);
    }
}
