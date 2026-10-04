using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Server.Ultima.Types.Targeting;

namespace Moongate.Server.Ultima.Commands;

/// <summary>
///     Sets a number of the mobile the game master targets: <c>set hits|mana|stamina|hunger|thirst|criminal &lt;value&gt;</c>. Hit
///     points, mana and stamina stay between 0 and their maximum, hunger and thirst between 0 and 20;
///     criminal makes a criminal of it with anything but 0, and pardons it with 0.
/// </summary>
public sealed class SetCommand : ICommandExecutor
{
    private const string Usage = "set <hits|mana|stamina|hunger|thirst|criminal> <value>";

    private readonly ITargetService _targets;
    private readonly IMobileService _mobiles;
    private readonly IMobileStateService _state;
    private readonly IHungerService _hunger;
    private readonly IGameLoopService _loop;
    private readonly ICrimeService? _crimes;
    private readonly ILocalizationService? _localization;

    public SetCommand(
        ITargetService targets,
        IMobileService mobiles,
        IMobileStateService state,
        IHungerService hunger,
        IGameLoopService loop,
        ICrimeService? crimes = null,
        ILocalizationService? localization = null
    )
    {
        _crimes = crimes;
        _localization = localization;
        _targets = targets;
        _mobiles = mobiles;
        _state = state;
        _hunger = hunger;
        _loop = loop;
    }

    public async Task ExecuteAsync(CommandContext context)
    {
        if (context.Session is not { } session)
        {
            context.PrintError("set works in game only.");

            return;
        }

        if (context.Arguments.Length != 2 ||
            context.Arguments[0].ToLowerInvariant() is not ("hits" or "mana" or "stamina" or "hunger" or "thirst" or "criminal") ||
            !int.TryParse(context.Arguments[1], out var value) ||
            value < 0)
        {
            context.PrintError(_localization.Text(CommandMessages.Usage, "Usage: {0}", Usage));

            return;
        }

        var what = context.Arguments[0].ToLowerInvariant();
        var target = await _targets.RequestAsync(
            session,
            TargetCursorType.Object,
            TargetFlagsType.Neutral,
            context.CancellationToken
        );

        if (target.Kind != TargetResultType.Object)
        {
            context.Print(_localization.Text(CommandMessages.TargetCanceled, "Target canceled."));

            return;
        }

        string? name = null;
        var now = 0;
        var work = new LoopActionWorkItem(
            () =>
            {
                if (!_mobiles.TryGet(target.Serial, out var mobile))
                {
                    return;
                }

                switch (what)
                {
                    case "hits":
                        _state.SetStats(mobile, new MobileStatsChange { Hits = value });
                        now = mobile.Hits;

                        break;
                    case "mana":
                        _state.SetStats(mobile, new MobileStatsChange { Mana = value });
                        now = mobile.Mana;

                        break;
                    case "stamina":
                        _state.SetStats(mobile, new MobileStatsChange { Stamina = value });
                        now = mobile.Stamina;

                        break;
                    case "criminal":
                        if (value == 0)
                        {
                            _crimes?.Pardon(mobile);
                        }
                        else
                        {
                            _crimes?.MakeCriminal(mobile);
                        }

                        now = mobile.Criminal ? 1 : 0;

                        break;
                    case "thirst":
                        _hunger.SetThirst(mobile, value);
                        now = mobile.Thirst;

                        break;
                    default:
                        _hunger.Set(mobile, value);
                        now = mobile.Hunger;

                        break;
                }

                name = mobile.Name;
            }
        );
        await _loop.PostAsync(work, context.CancellationToken);
        await work.Completion;

        if (name is null)
        {
            context.Print(_localization.Text(CommandMessages.NotAMobile, "That is not a character or an NPC."));

            return;
        }

        context.Print(_localization.Text(CommandMessages.StatSet, "{0}: {1} is now {2}.", name, what, now));
    }
}
