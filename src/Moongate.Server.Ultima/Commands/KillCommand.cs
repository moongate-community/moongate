using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Server.Ultima.Types.Targeting;

namespace Moongate.Server.Ultima.Commands;

/// <summary>
///     Kills the NPC the game master targets: it dies where it stands and leaves its corpse, as when something kills
///     it. A player dies too and stays as a ghost.
/// </summary>
public sealed class KillCommand : ICommandExecutor
{
    private readonly IDeathService _death;
    private readonly ITargetService _targets;
    private readonly IMobileService _mobiles;
    private readonly IGameLoopService _loop;
    private readonly ILocalizationService? _localization;

    public KillCommand(
        IDeathService death,
        ITargetService targets,
        IMobileService mobiles,
        IGameLoopService loop,
        ILocalizationService? localization = null
    )
    {
        _death = death;
        _targets = targets;
        _mobiles = mobiles;
        _loop = loop;
        _localization = localization;
    }

    public async Task ExecuteAsync(CommandContext context)
    {
        if (context.Session is not { } session)
        {
            context.PrintError("kill works in game only.");

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
            context.Print(_localization.Text(CommandMessages.TargetCanceled, "Target canceled."));

            return;
        }

        // On the game loop, where the mobiles live.
        var answer = "";
        var work = new LoopActionWorkItem(() =>
            {
                if (!_mobiles.TryGet(target.Serial, out var mobile) || !_mobiles.IsInWorld(mobile.Id))
                {
                    answer = _localization.Text(CommandMessages.NotAnNpc, "That is not an NPC.");

                    return;
                }

                var name = mobile.Name ?? "";
                _mobiles.TryGet(session.CharacterId, out var killer);
                answer = _death.Kill(mobile, killer) ? _localization.Text(CommandMessages.Killed, "{0} is dead.", name) :
                    mobile.IsNpc ? _localization.Text(CommandMessages.NotAnNpc, "That is not an NPC.") :
                    _localization.Text(CommandMessages.CannotDie, "{0} cannot die.", name);
            }
        );
        await _loop.PostAsync(work, context.CancellationToken);
        await work.Completion;
        context.Print(answer);
    }
}
