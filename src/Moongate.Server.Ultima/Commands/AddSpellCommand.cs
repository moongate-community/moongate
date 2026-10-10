using System.Numerics;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Commands.Internal;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Server.Ultima.Types.Targeting;

namespace Moongate.Server.Ultima.Commands;

/// <summary>
///     <c>add_spell &lt;key | number | circle N | all&gt;</c>: writes a spell, the spells of a circle or all 64 in the
///     spellbook a game master targets, or in the one a targeted character or NPC wears or carries in its backpack, and
///     says how many were new. The spells come from the catalog: one with no script is added all the same.
/// </summary>
public sealed class AddSpellCommand : ICommandExecutor
{
    private const string UsageText = "add_spell <key | number | circle <1..8> | all>";

    private readonly ITargetService _targets;
    private readonly ISpellCatalogService _catalog;
    private readonly ISpellbookService _books;
    private readonly IItemService _items;
    private readonly IMobileService _mobiles;
    private readonly IGameLoopService _loop;
    private readonly ILocalizationService? _localization;

    public AddSpellCommand(
        ITargetService targets,
        ISpellCatalogService catalog,
        ISpellbookService books,
        IItemService items,
        IMobileService mobiles,
        IGameLoopService loop,
        ILocalizationService? localization = null
    )
    {
        _targets = targets;
        _catalog = catalog;
        _books = books;
        _items = items;
        _mobiles = mobiles;
        _loop = loop;
        _localization = localization;
    }

    public async Task ExecuteAsync(CommandContext context)
    {
        if (context.Session is not { } session)
        {
            context.PrintError("add_spell works in game only.");

            return;
        }

        var failure = SpellSelection.TryRead(_catalog, context.Arguments, out var spells, out var used, out var detail);

        if (failure != SpellSelection.Failure.None || used != context.Arguments.Length)
        {
            context.PrintError(Describe(failure, detail, UsageText));

            return;
        }

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

        // The book is written and shown again on the loop; commands run off it.
        string? carrier = null;
        var isBook = false;
        var isMobile = false;
        var added = 0;
        var held = 0;
        var work = new LoopActionWorkItem(() =>
            {
                ItemEntity? book = _items.TryGet(target.Serial, out var item) && _books.IsSpellbook(item) ? item : null;

                if (book is null && _mobiles.TryGet(target.Serial, out var mobile))
                {
                    isMobile = true;
                    carrier = mobile.Name;
                    book = _books.FindCarried(mobile, 0);
                }

                if (book is null)
                {
                    return;
                }

                isBook = true;

                // One write and one re-send for all the spells, not one for each.
                added = _books.Add(book, spells.Select(spell => spell.Id), session);

                held = BitOperations.PopCount(_books.GetSpells(book));
            }
        );
        await _loop.PostAsync(work, context.CancellationToken);
        await work.Completion;

        if (isBook)
        {
            context.Print(
                _localization.Text(
                    CommandMessages.SpellsAdded,
                    "Spells added to the spellbook: {0} new, {1} in the book now.",
                    added,
                    held
                )
            );
        }
        else if (isMobile)
        {
            context.Print(_localization.Text(CommandMessages.NoSpellbook, "{0} carries no spellbook.", carrier ?? ""));
        }
        else
        {
            context.Print(
                _localization.Text(
                    CommandMessages.NotASpellbookOrMobile,
                    "That is not a spellbook, a character or an NPC."
                )
            );
        }
    }

    private string Describe(SpellSelection.Failure failure, string detail, string usage)
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
            _ => _localization.Text(CommandMessages.Usage, "Usage: {0}", usage)
        };
    }
}
