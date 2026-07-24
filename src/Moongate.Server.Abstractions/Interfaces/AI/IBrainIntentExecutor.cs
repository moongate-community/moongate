using Moongate.Core.Primitives;
using Moongate.Server.Abstractions.Data.AI;

namespace Moongate.Server.Abstractions.Interfaces.AI;

/// <summary>Applies validated brain intents to world state.</summary>
public interface IBrainIntentExecutor
{
    /// <summary>Executes the supplied intents for a mobile in the given brain context.</summary>
    void Execute(Serial mobileId, BrainContext context, IReadOnlyList<BrainIntent> intents);
}
