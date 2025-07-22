using Jint.Native;
using Moongate.Core.Server.Interfaces.Services;
using Moongate.UO.Data.Contexts;
using Moongate.UO.Data.Interfaces.Ai;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Wraps;

public class AiBrainWrap : BaseWrap, IAiBrainAction
{

    public AiBrainWrap(
        IScriptEngineService scriptEngineService, JsValue executeCallback
    ) : base(scriptEngineService, executeCallback)
    {
    }

    public void Execute(AiContext context)
    {
        Call(nameof(Execute), context);
    }

    public void ReceiveSpeech(AiContext context, string speech, UOMobileEntity from)
    {
        Call(nameof(ReceiveSpeech), context, speech, from);
    }
}
