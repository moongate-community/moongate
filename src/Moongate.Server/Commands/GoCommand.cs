using System.Globalization;
using Moongate.Core.Types;
using Moongate.Server.Abstractions.Attributes;
using Moongate.Server.Abstractions.Data.Commands;
using Moongate.Server.Abstractions.Interfaces.Commands;
using Moongate.Server.Abstractions.Interfaces.Mobiles;
using Moongate.Server.Abstractions.Types;

namespace Moongate.Server.Commands;

/// <summary>
/// Moves the character who typed it to a coordinate on its current map.
/// <para>
/// It exists because nothing else could reach anywhere. World decoration, signs and doors all shipped
/// without one being looked at in a client — not because they were broken, but because a character
/// logs in where it logs in, the world is 6,144 tiles across, and the nearest building was ninety-nine
/// tiles away through a forest. Every one of those features was verified through the item store and
/// the spatial index instead, which proves they are in the world and nothing about what a player sees.
/// </para>
/// </summary>
[Command(
    "go",
    AccountLevelType.GrandMaster,
    "Moves you to x y [z] on your current map.",
    Sources = CommandSourceType.InGame
)]
public sealed class GoCommand : ICommand
{
    private readonly IMobileService _mobiles;

    public GoCommand(IMobileService mobiles)
    {
        _mobiles = mobiles;
    }

    public void Execute(CommandContext context)
    {
        // A console has nobody to move, and saying so beats moving nobody in silence.
        if (context.Actor is not { } actor)
        {
            context.Reply("This command needs a player: there is nobody here to move.");

            return;
        }

        if (context.Arguments.Count < 2)
        {
            context.Reply("Usage: go <x> <y> [z]");

            return;
        }

        if (!TryReadCoordinates(context.Arguments, out var x, out var y, out var z))
        {
            context.Reply("Coordinates have to be a number: go <x> <y> [z]");

            return;
        }

        // Teleport, not a walk: it does not validate the terrain, which is the point. A GM asked to
        // be somewhere, and being unable to get there is what this command exists to fix.
        if (!_mobiles.Teleport(actor.Id, x, y, z))
        {
            context.Reply("Could not move you there. Check the server log.");

            return;
        }

        context.Reply(string.Create(CultureInfo.InvariantCulture, $"Moved to {x}, {y}, {z}."));
    }

    /// <summary>Reads x, y and an optional z; false when any of them is not a number.</summary>
    private static bool TryReadCoordinates(IReadOnlyList<string> arguments, out int x, out int y, out int z)
    {
        y = 0;
        z = 0;

        return int.TryParse(arguments[0], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out x) &&
               int.TryParse(arguments[1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out y) &&
               (arguments.Count < 3 ||
                int.TryParse(arguments[2], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out z));
    }
}
