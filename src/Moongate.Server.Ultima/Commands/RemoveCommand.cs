using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types.Targeting;

namespace Moongate.Server.Ultima.Commands;

/// <summary>
///     Removes the NPC the game master targets; players and items are refused.
/// </summary>
public sealed class RemoveCommand : ICommandExecutor
{
    private readonly INpcService _npcs;
    private readonly ITargetService _targets;

    public RemoveCommand(INpcService npcs, ITargetService targets)
    {
        _npcs = npcs;
        _targets = targets;
    }

    public async Task ExecuteAsync(CommandContext context)
    {
        if (context.Session is not { } session)
        {
            context.PrintError("remove works in game only.");

            return;
        }

        var target = await _targets.RequestAsync(
            session,
            TargetCursorType.Object,
            TargetFlagsType.Harmful,
            context.CancellationToken
        );

        if (target.Kind != TargetResultType.Object)
        {
            context.Print("Canceled.");

            return;
        }

        if (await _npcs.RemoveAsync(target.Serial, context.CancellationToken))
        {
            context.Print("Removed {0}.", target.Serial);
        }
        else
        {
            context.Print("Not an NPC.");
        }
    }
}
