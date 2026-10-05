using System.Globalization;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Server.Ultima.Types.Targeting;

namespace Moongate.Server.Ultima.Commands;

/// <summary>
///     <c>add_gold &lt;amount&gt;</c>: puts a pile of that many gold coins, made out of nothing, in the backpack of
///     the character or NPC a game master targets. The amount is checked before the target cursor opens.
/// </summary>
public sealed class AddGoldCommand : ICommandExecutor
{
    private const string UsageText = "add_gold <1..60000>";

    private readonly ITargetService _targets;
    private readonly IItemHandlingService _handling;
    private readonly IMobileService _mobiles;
    private readonly ISessionService _sessions;
    private readonly IGameLoopService _loop;
    private readonly ItemsConfig _itemsConfig;
    private readonly IFatigueService? _fatigue;
    private readonly ILocalizationService? _localization;

    public AddGoldCommand(
        ITargetService targets,
        IItemHandlingService handling,
        IMobileService mobiles,
        ISessionService sessions,
        IGameLoopService loop,
        ItemsConfig itemsConfig,
        IFatigueService? fatigue = null,
        ILocalizationService? localization = null
    )
    {
        _targets = targets;
        _handling = handling;
        _mobiles = mobiles;
        _sessions = sessions;
        _loop = loop;
        _itemsConfig = itemsConfig;
        _fatigue = fatigue;
        _localization = localization;
    }

    public async Task ExecuteAsync(CommandContext context)
    {
        if (context.Session is not { } session)
        {
            context.PrintError("add_gold works in game only.");

            return;
        }

        // One pile: a larger sum is a bank check (create_check).
        if (context.Arguments.Length != 1 ||
            !int.TryParse(context.Arguments[0], NumberStyles.None, CultureInfo.InvariantCulture, out var amount) ||
            amount < 1 ||
            amount > BankService.PileMaximum)
        {
            context.PrintError(_localization.Text(CommandMessages.Usage, "Usage: {0}", UsageText));

            return;
        }

        var target = await _targets.RequestAsync(session, TargetCursorType.Object, TargetFlagsType.Neutral, context.CancellationToken);

        if (target.Kind != TargetResultType.Object)
        {
            context.Print(_localization.Text(CommandMessages.TargetCanceled, "Target canceled."));

            return;
        }

        // Items are made and given on the loop; commands run off it.
        string? name = null;
        var found = false;
        var given = false;
        var work = new LoopActionWorkItem(
            () =>
            {
                if (!_mobiles.TryGet(target.Serial, out var mobile))
                {
                    return;
                }

                found = true;
                name = mobile.Name;

                if (_handling.Give(mobile, _itemsConfig.GoldTemplate, amount) is null)
                {
                    return;
                }

                given = true;

                // Gold weighs: a player sees its new load.
                if (_fatigue is not null && _sessions.TryGetByCharacterId(mobile.Id, out var owner))
                {
                    _fatigue.LoadChanged(owner, mobile, false);
                }
            }
        );
        await _loop.PostAsync(work, context.CancellationToken);
        await work.Completion;

        if (!found)
        {
            context.Print(_localization.Text(CommandMessages.NotAMobile, "That is not a character or an NPC."));

            return;
        }

        if (!given)
        {
            context.PrintError(_localization.Text(CommandMessages.GoldNotAdded, "{0} has no backpack, or it is full.", name ?? ""));

            return;
        }

        context.Print(
            _localization.Text(
                CommandMessages.GoldAdded,
                "{1} gold is in the backpack of {0}.",
                name ?? "",
                amount.ToString("N0", CultureInfo.InvariantCulture)
            )
        );
    }
}
