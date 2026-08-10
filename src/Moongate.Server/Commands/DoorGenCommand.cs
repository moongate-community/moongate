using System.Globalization;
using Moongate.Core.Types;
using Moongate.Server.Abstractions.Attributes;
using Moongate.Server.Abstractions.Data.Commands;
using Moongate.Server.Abstractions.Interfaces.Commands;
using Moongate.Server.Abstractions.Types;
using Moongate.Server.Services.World;

namespace Moongate.Server.Commands;

/// <summary>
/// Puts doors in the doorways the map art draws and leaves empty.
/// <para>
/// Almost every door a player meets is one of those: the bank, the shops, every house. They are not
/// in the decoration corpus — the art draws two frames with a gap between them and nothing in the gap
/// — so <c>decorate</c> never touches them and this finds them instead.
/// </para>
/// <para>
/// It takes minutes rather than seconds, because it reads 21.4 million tiles. Unlike
/// <c>decorate</c> it does <b>not</b> stop the world while it runs: the reading happens beside the
/// game loop and only the door creation is marshalled onto it, in batches. The command answers
/// straight away and reports when the work is done.
/// </para>
/// </summary>
[Command(
    "doorgen",
    AccountLevelType.Administrator,
    "Puts doors in the doorways the map art leaves empty.",
    Sources = CommandSourceType.InGame | CommandSourceType.Console
)]
public sealed class DoorGenCommand : ICommand
{
    private readonly DoorGenerationService _doors;

    public DoorGenCommand(DoorGenerationService doors)
    {
        _doors = doors;
    }

    public void Execute(CommandContext context)
    {
        var reply = context.Reply;

        reply("Scanning 21.4 million tiles for empty doorways. This takes minutes; the world keeps running.");

        // Not awaited: the scan is far longer than any command should hold its caller for, and the
        // reply delegate is what carries the answer back whenever it arrives.
        _ = Task.Run(
            async () =>
            {
                try
                {
                    var (placed, skipped, scanned) = await _doors.GenerateAsync();

                    reply(
                        string.Create(
                            CultureInfo.InvariantCulture,
                            $"Doors done: placed {placed}, skipped {skipped} already there, from {scanned} tile(s)."
                        )
                    );
                }
                catch (Exception exception)
                {
                    reply($"Door generation failed: {exception.Message}. Check the server log.");
                }
            }
        );
    }
}
