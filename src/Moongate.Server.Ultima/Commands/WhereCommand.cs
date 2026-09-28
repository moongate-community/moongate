using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types.Targeting;

namespace Moongate.Server.Ultima.Commands;

/// <summary>
///     Asks for a target and prints what was picked: the serial of an item or a mobile, or the map and location of a
///     spot on the ground or on a static.
/// </summary>
public sealed class WhereCommand : ICommandExecutor
{
    private readonly ITargetService _targets;

    public WhereCommand(ITargetService targets)
    {
        _targets = targets;
    }

    public async Task ExecuteAsync(CommandContext context)
    {
        if (context.Session is not { } session)
        {
            context.PrintError("where works in game only.");

            return;
        }

        var result = await _targets.RequestAsync(
            session,
            TargetCursorType.Location,
            TargetFlagsType.Neutral,
            context.CancellationToken
        );

        switch (result.Kind)
        {
            case TargetResultType.Object:
                context.Print("{0}", result.Serial);

                break;
            case TargetResultType.Location:
                context.Print("{0} ({1}, {2}, {3})", result.Map, result.Location.X, result.Location.Y, result.Location.Z);

                break;
            default:
                context.Print("Target canceled.");

                break;
        }
    }
}
