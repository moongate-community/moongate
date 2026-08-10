using System.Globalization;
using Moongate.Core.Types;
using Moongate.Server.Abstractions.Attributes;
using Moongate.Server.Abstractions.Data.Commands;
using Moongate.Server.Abstractions.Interfaces.Commands;
using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.Server.Abstractions.Types;
using Moongate.Server.Services.World;

namespace Moongate.Server.Commands;

/// <summary>
/// Places the world's catalogued decoration as real items.
/// <para>
/// The placing itself belongs to <see cref="DecorationPlacementService" />; what this adds is the
/// reporting. Forty thousand objects placed in silence cannot be told apart from nothing happening,
/// so the command says how many it is about to attempt and then how many it placed and how many were
/// already standing there — the second number being what tells a repeat run from a first one.
/// </para>
/// <para>
/// It runs the placement inline, and doing so stalls the world for as long as it takes: every object
/// is a persisted upsert, and commands already run on the single game-loop thread (every source posts
/// there before dispatching). That is inherent to placing this much in one go rather than something
/// the command works around, which is why the acknowledgement warns about it out loud.
/// </para>
/// </summary>
[Command(
    "decorate",
    AccountLevelType.Administrator,
    "Places the world's catalogued decoration.",
    Sources = CommandSourceType.InGame | CommandSourceType.Console
)]
public sealed class DecorateCommand : ICommand
{
    private readonly IDecorationCatalog _decorations;
    private readonly DecorationPlacementService _placement;
    private readonly ISignService _signs;

    public DecorateCommand(
        IDecorationCatalog decorations,
        DecorationPlacementService placement,
        ISignService signs
    )
    {
        _decorations = decorations;
        _placement = placement;
        _signs = signs;
    }

    public void Execute(CommandContext context)
    {
        // Signs count too: a shard whose decoration is already down but whose signs are not would
        // otherwise be told there is nothing to place.
        var total = _decorations.All.Count + _signs.Count;

        // An empty catalogue means the loader found nothing at boot. Saying that here is cheaper than
        // reading the log to find out why "placed 0" was not the idempotent answer it looked like.
        if (total == 0)
        {
            context.Reply("Nothing to place: the decoration catalogue is empty.");

            return;
        }

        context.Reply(
            string.Create(
                CultureInfo.InvariantCulture,
                $"Placing {total} catalogued decoration object(s). The world pauses while it runs."
            )
        );

        var (placed, skipped, converted) = _placement.Place();

        // Nothing placed, nothing already there and nothing converted, out of a catalogue that holds
        // objects: whatever went wrong, reporting it as "done" would read exactly like an idempotent
        // second run. That is how the missing-template failure presented on a real shard -- the reason
        // sat in the server log while the operator was told the job had finished. Converting counts as
        // having done something, or a run that repaired every door would report itself as broken.
        if (placed == 0 && skipped == 0 && converted == 0)
        {
            context.Reply(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Placed nothing of {total} object(s), and none were already there. Check the server log."
                )
            );

            return;
        }

        // Only mentioned when it happened: a line about 0 converted doors on every run is noise, and
        // the number is only interesting the once, on the shard that needed repairing.
        var conversion = converted == 0
                             ? string.Empty
                             : string.Create(
                                 CultureInfo.InvariantCulture,
                                 $" Converted {converted} already-placed door(s)."
                             );

        context.Reply(
            string.Create(
                CultureInfo.InvariantCulture,
                $"Decoration done: placed {placed}, skipped {skipped} already present.{conversion}"
            )
        );
    }
}
