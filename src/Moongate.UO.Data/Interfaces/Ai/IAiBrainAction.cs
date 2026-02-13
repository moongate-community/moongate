using Moongate.UO.Data.Contexts;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.UO.Data.Interfaces.Ai;

public interface IAiBrainAction
{
    /// <summary>
    /// Executes the AI brain action.
    /// </summary>
    /// <param name="context">The context containing necessary information for the action.</param>
    void Execute(AiContext context);

    /// <summary>
    /// Receives speech from a mobile entity.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="speech"></param>
    /// <param name="from"></param>
    void ReceiveSpeech(AiContext context, string speech, UOMobileEntity from);
}
