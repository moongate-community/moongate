using Moongate.UO.Data.Contexts;

namespace Moongate.UO.Data.Interfaces.Ai;

public interface IAiBrainAction
{
    /// <summary>
    /// Executes the AI brain action.
    /// </summary>
    /// <param name="context">The context containing necessary information for the action.</param>
    void Execute(AiContext context);

}
