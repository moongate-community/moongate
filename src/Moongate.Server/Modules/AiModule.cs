using Moongate.Core.Server.Attributes.Scripts;
using Moongate.Server.Wraps;
using Moongate.UO.Data.Interfaces.Services;

namespace Moongate.Server.Modules;

[ScriptModule("ai")]
public class AiModule
{
    private readonly IAiService _aiService;

    public AiModule(IAiService aiService)
    {
        _aiService = aiService;
    }


    [ScriptFunction("Add brain")]
    public void AddBrain(string brainId, AiBrainWrap aiBrainWrap)
    {
        _aiService.AddBrain(
            brainId,
            aiBrainWrap
        );
    }
}
