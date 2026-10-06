using System.Globalization;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Items;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Services.Internal;

namespace Moongate.Server.Ultima.Commands;

/// <summary>
///     <c>create_check &lt;amount&gt;</c>: puts in the game master's backpack a bank check worth that much gold,
///     made out of nothing. It is not held to the smallest and largest check a banker writes.
/// </summary>
public sealed class CreateCheckCommand : ICommandExecutor
{
    public const int MaximumWorth = 2_000_000_000;

    private const string UsageText = "create_check <1..2000000000>";

    private readonly IItemHandlingService _handling;
    private readonly IMobileService _mobiles;
    private readonly IGameLoopService _loop;
    private readonly ILocalizationService? _localization;

    public CreateCheckCommand(
        IItemHandlingService handling,
        IMobileService mobiles,
        IGameLoopService loop,
        ILocalizationService? localization = null
    )
    {
        _handling = handling;
        _mobiles = mobiles;
        _loop = loop;
        _localization = localization;
    }

    public async Task ExecuteAsync(CommandContext context)
    {
        if (context.Session is not { } session)
        {
            context.PrintError("create_check works in game only.");

            return;
        }

        if (context.Arguments.Length != 1 ||
            !int.TryParse(context.Arguments[0], NumberStyles.None, CultureInfo.InvariantCulture, out var worth) ||
            worth < 1 ||
            worth > MaximumWorth)
        {
            context.PrintError(_localization.Text(CommandMessages.Usage, "Usage: {0}", UsageText));

            return;
        }

        // Items are made and given on the loop; commands run off it.
        var made = false;
        var work = new LoopActionWorkItem(() =>
            {
                if (_mobiles.TryGet(session.CharacterId, out var character) &&
                    _handling.Give(character, BankService.CheckTemplate) is { } check)
                {
                    check.SetProp(ItemPropKeys.BankWorth, (long)worth);
                    check.SetProp(ItemPropKeys.LabelNumber, (long)BankService.CheckLabel);
                    _handling.Refresh(check);
                    made = true;
                }
            }
        );
        await _loop.PostAsync(work, context.CancellationToken);
        await work.Completion;

        if (!made)
        {
            context.PrintError(
                _localization.Text(
                    CommandMessages.CheckNotCreated,
                    "No check was made: you have no backpack, or it is full."
                )
            );

            return;
        }

        context.Print(
            _localization.Text(
                CommandMessages.CheckCreated,
                "A bank check worth {0} gold is in your backpack.",
                worth.ToString("N0", CultureInfo.InvariantCulture)
            )
        );
    }
}
