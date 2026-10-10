using Moongate.Persistence.Interfaces;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Ultima.Commands;

/// <summary>
///     Prints when the player characters of a name were last online, in the world or not:
///     <c>
///         lastonline &lt;name&gt;
///     </c>
///     . One in the world is online now; one that has not left the world since the date
///     began to be recorded has no date yet.
/// </summary>
public sealed class LastOnlineCommand : ICommandExecutor
{
    private const string UsageText = "lastonline <name>";
    private const string DateFormat = "yyyy-MM-dd HH:mm";

    private readonly IDataAccess<MobileEntity> _characters;
    private readonly IMobileService _mobiles;
    private readonly ILocalizationService? _localization;

    public LastOnlineCommand(
        IDataAccess<MobileEntity> characters,
        IMobileService mobiles,
        ILocalizationService? localization = null
    )
    {
        _characters = characters;
        _mobiles = mobiles;
        _localization = localization;
    }

    public async Task ExecuteAsync(CommandContext context)
    {
        // A name of several words comes as several arguments.
        var name = string.Join(' ', context.Arguments).Trim();

        if (name.Length == 0)
        {
            context.PrintError(_localization.Text(CommandMessages.Usage, "Usage: {0}", UsageText));

            return;
        }

        var wanted = name.ToLowerInvariant();

        // The database lowers the name: this ToLower becomes its lower(), where a culture means nothing.
#pragma warning disable CA1311
        var found = await _characters.QueryAsync(
            mobile => mobile.AccountId != null && mobile.DeletionRequestedAt == null && mobile.Name.ToLower() == wanted,
            context.CancellationToken
        );
#pragma warning restore CA1311

        if (found.Count == 0)
        {
            context.PrintError(_localization.Text(CommandMessages.JailNobodyNamed, "No character is named {0}.", name));

            return;
        }

        foreach (var character in found.OrderBy(character => character.Id.Value))
        {
            if (_mobiles.IsInWorld(character.Id))
            {
                context.Print(_localization.Text(CommandMessages.LastOnlineNow, "{0} is online now.", character.Name));
            }
            else if (character.LastOnlineAt is { } at)
            {
                context.Print(
                    _localization.Text(
                        CommandMessages.LastOnlineAt,
                        "{0} was last online on {1} (UTC).",
                        character.Name,
                        at.ToString(DateFormat)
                    )
                );
            }
            else
            {
                context.Print(
                    _localization.Text(
                        CommandMessages.LastOnlineNever,
                        "{0} has no last online date yet: it has not left the world since the date began to be recorded.",
                        character.Name
                    )
                );
            }
        }
    }
}
