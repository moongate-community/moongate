using System.Globalization;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Commands.Internal;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services.Internal;

namespace Moongate.Server.Ultima.Commands;

/// <summary>
///     <c>add_reagents &lt;key | number | circle N | all&gt; [amount]</c>: puts the reagents of a spell, of the spells of
///     a circle or of all of them in the backpack of the game master who runs it, <c>amount</c> of each, one stack per
///     reagent kind. A stack the backpack cannot take lies at the game master's feet.
/// </summary>
public sealed class AddReagentsCommand : ICommandExecutor
{
    public const int DefaultAmount = 20;
    public const int MaxAmount = 1000;

    private const string UsageText = "add_reagents <key | number | circle <1..8> | all> [1..1000]";

    private readonly ISpellCatalogService _catalog;
    private readonly IItemTemplateService _templates;
    private readonly IItemHandlingService _handling;
    private readonly IItemService _items;
    private readonly IWorldViewService _view;
    private readonly IMobileService _mobiles;
    private readonly IGameLoopService _loop;
    private readonly ILocalizationService? _localization;

    public AddReagentsCommand(
        ISpellCatalogService catalog,
        IItemTemplateService templates,
        IItemHandlingService handling,
        IItemService items,
        IWorldViewService view,
        IMobileService mobiles,
        IGameLoopService loop,
        ILocalizationService? localization = null
    )
    {
        _catalog = catalog;
        _templates = templates;
        _handling = handling;
        _items = items;
        _view = view;
        _mobiles = mobiles;
        _loop = loop;
        _localization = localization;
    }

    public async Task ExecuteAsync(CommandContext context)
    {
        if (context.Session is not { } session)
        {
            context.PrintError("add_reagents works in game only.");

            return;
        }

        var failure = SpellSelection.TryRead(_catalog, context.Arguments, out var spells, out var used, out var detail);
        var amount = DefaultAmount;
        var rest = context.Arguments.Length - used;

        if (failure == SpellSelection.Failure.None &&
            (rest > 1 ||
             (rest == 1 &&
              (!int.TryParse(
                   context.Arguments[used],
                   NumberStyles.None,
                   CultureInfo.InvariantCulture,
                   out amount
               ) ||
               amount is < 1 or > MaxAmount))))
        {
            failure = SpellSelection.Failure.Usage;
        }

        if (failure != SpellSelection.Failure.None)
        {
            context.PrintError(Describe(failure, detail));

            return;
        }

        // One stack per reagent kind, in the order the spells name them.
        var reagents = spells.SelectMany(spell => spell.Reagents)
            .Select(reagent => reagent.Template)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var kept = new List<string>();
        var dropped = new List<string>();

        // Items are made, given and put on the ground on the loop; commands run off it.
        var work = new LoopActionWorkItem(() =>
            {
                if (!_mobiles.TryGet(session.CharacterId, out var gm))
                {
                    return;
                }

                foreach (var template in reagents)
                {
                    var name = _templates.TryGet(template, out var found) ? found.Name ?? template : template;

                    if (_handling.Give(gm, template, amount) is not null)
                    {
                        kept.Add(name);
                    }
                    else if (_handling.Make(template, amount) is { } pile)
                    {
                        pile.PlaceOnGround(gm.Map, gm.Location);
                        _items.Add([pile]);
                        _view.ItemAppeared(pile);
                        dropped.Add(name);
                    }
                }
            }
        );
        await _loop.PostAsync(work, context.CancellationToken);
        await work.Completion;

        if (kept.Count == 0 && dropped.Count == 0)
        {
            context.PrintError(
                _localization.Text(CommandMessages.NoReagents, "No reagent could be made for those spells.")
            );

            return;
        }

        if (kept.Count > 0)
        {
            context.Print(
                _localization.Text(
                    CommandMessages.ReagentsAdded,
                    "Reagents in your backpack, {0} of each: {1}.",
                    amount,
                    string.Join(", ", kept)
                )
            );
        }

        if (dropped.Count > 0)
        {
            context.Print(
                _localization.Text(
                    CommandMessages.ReagentsAtFeet,
                    "{0} of each did not fit the backpack and lie at your feet: {1}.",
                    amount,
                    string.Join(", ", dropped)
                )
            );
        }
    }

    private string Describe(SpellSelection.Failure failure, string detail)
    {
        return failure switch
        {
            SpellSelection.Failure.UnknownSpell => _localization.Text(
                CommandMessages.UnknownSpell,
                "Unknown spell: {0}",
                detail
            ),
            SpellSelection.Failure.UnknownCircle => _localization.Text(
                CommandMessages.UnknownCircle,
                "Unknown circle: {0}. A circle is a number from 1 to 8.",
                detail
            ),
            _ => _localization.Text(CommandMessages.Usage, "Usage: {0}", UsageText)
        };
    }
}
