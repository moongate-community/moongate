using Jint.Native;
using Moongate.Core.Server.Interfaces.Services;
using Moongate.UO.Data.Contexts;
using Moongate.UO.Data.Interfaces.Ai;

namespace Moongate.Server.Wraps;

public class AiBrainWrap : BaseWrap, IAiBrainAction
{
    public AiBrainWrap(IScriptEngineService scriptEngineService, JsValue jsValue) : base(scriptEngineService, jsValue)
    {
    }

    public void Execute(AiContext context)
    {
        Call(nameof(Execute), context);
    }
}
