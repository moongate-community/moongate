using System.Globalization;
using Moongate.Core.Primitives;
using Moongate.Core.Types;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Attributes;
using Moongate.Server.Abstractions.Data.Commands;
using Moongate.Server.Abstractions.Data.Session;
using Moongate.Server.Abstractions.Data.World;
using Moongate.Server.Abstractions.Interfaces.Accounts;
using Moongate.Server.Abstractions.Interfaces.Commands;
using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.Server.Abstractions.Types;
using Moongate.Server.Abstractions.Types.World;
using Moongate.UO.Data.Types;
using SquidStd.Persistence.Abstractions.Interfaces.Persistence;

namespace Moongate.Server.Commands;

/// <summary>
/// Raises a target cursor and says what was clicked, to whoever ran it.
/// <para>
/// The first consumer of the targeting primitive, and deliberately a probe rather than a feature:
/// it exercises both selection modes, which is what makes it a better test of the mechanism than a
/// command a GM would actually reach for.
/// </para>
/// </summary>
[Command(
    "where",
    AccountLevelType.GrandMaster,
    "Raises a target cursor and reports what was clicked.",
    Sources = CommandSourceType.InGame
)]
public sealed class WhereCommand : ICommand
{
    private readonly IPlayerTargetService _targets;
    private readonly ISessionManager _sessions;
    private readonly IEntityStore<MobileEntity, Serial> _mobiles;

    public WhereCommand(
        IPlayerTargetService targets,
        ISessionManager sessions,
        IPersistenceService persistenceService
    )
    {
        _targets = targets;
        _sessions = sessions;
        _mobiles = persistenceService.GetStore<MobileEntity, Serial>();
    }

    public void Execute(CommandContext context)
    {
        // A console has no cursor to raise. Saying so beats raising one for nobody.
        if (context.Actor is not { } actor)
        {
            context.Reply("This command needs a player: there is no cursor to raise from here.");

            return;
        }

        if (SessionFor(actor.Id) is not { } session)
        {
            context.Reply("You are not in the world.");

            return;
        }

        var reply = context.Reply;

        _targets.Request(session, TargetSelectionType.Object, result => reply(Describe(result)));
    }

    private string Describe(TargetResult result)
    {
        if (result.IsCancelled)
        {
            return "Nothing targeted.";
        }

        var where = string.Create(
            CultureInfo.InvariantCulture,
            $"{result.Location.X}, {result.Location.Y}, {result.Location.Z}"
        );

        if (result.Type != TargetResultType.Object)
        {
            return $"Ground at {where}.";
        }

        var name = _mobiles.GetById(result.Serial)?.Name;

        return name is null
                   ? string.Create(CultureInfo.InvariantCulture, $"Object 0x{result.Serial.Value:X} at {where}.")
                   : string.Create(CultureInfo.InvariantCulture, $"{name} (0x{result.Serial.Value:X}) at {where}.");
    }

    private PlayerSession? SessionFor(Serial mobile)
        => _sessions.All.FirstOrDefault(session => session.Character?.Id == mobile);
}
