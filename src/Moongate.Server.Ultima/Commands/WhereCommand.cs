using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Interfaces.Services;
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
    private readonly ILocalizationService? _localization;

    public WhereCommand(ITargetService targets, ILocalizationService? localization = null)
    {
        _localization = localization;
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
                var spot = result.Location;
                context.Print("{0} ({1}, {2}, {3})", result.Map, spot.X, spot.Y, spot.Z);

                break;
            default:
                context.Print(_localization.Text(CommandMessages.TargetCanceled, "Target canceled."));

                break;
        }
    }
}
