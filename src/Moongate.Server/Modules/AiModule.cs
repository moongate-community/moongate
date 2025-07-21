using Jint.Native;
using Moongate.Core.Server.Attributes.Scripts;
using Moongate.Core.Server.Interfaces.Services;
using Moongate.Server.Utils;
using Moongate.Server.Wraps;
using Moongate.UO.Data.Contexts;
using Moongate.UO.Data.Interfaces.Ai;
using Moongate.UO.Data.Interfaces.Services;

namespace Moongate.Server.Modules;

[ScriptModule("ai")]
public class AiModule
{
    private readonly IAiService _aiService;

    private readonly IScriptEngineService _scriptEngineService;

    public AiModule(IAiService aiService, IScriptEngineService scriptEngineService)
    {
        _aiService = aiService;
        _scriptEngineService = scriptEngineService;
    }


    [ScriptFunction("Add brain")]
    public void AddBrain(string brainId, JsValue classz)
    {

        JsInteropUtils.ImplementsInterface<IAiBrainAction>(classz, _scriptEngineService);

        var aiBrainWrap = new AiBrainWrap(_scriptEngineService, classz);

        _aiService.AddBrain(
            brainId,
            aiBrainWrap
        );
    }

    [ScriptFunction("Add brain action")]
    public void AddBrainAction(string brainId, Action<AiContext> action)
    {
        var aiBrainWrap = new AiBrainFuncWrap(action);

        _aiService.AddBrain(
            brainId,
            aiBrainWrap
        );
    }
}
