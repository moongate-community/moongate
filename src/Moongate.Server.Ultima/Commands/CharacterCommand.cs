using System.Globalization;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces;
using Serilog;

namespace Moongate.Server.Ultima.Commands;

/// <summary>
///     Staff operations on player characters: list the ones pending deletion and restore them.
/// </summary>
public sealed class CharacterCommand : ICommandExecutor, ICommandArgumentCompleter
{
    private const string Syntax = "character pending [account-serial] | character restore <character-serial>";
    private const string TimeFormat = "yyyy-MM-dd HH:mm";

    private readonly ILogger _logger = Log.ForContext<CharacterCommand>();
    private readonly ICharacterService _characters;
    private readonly CharactersConfig _config;
    private readonly ILocalizationService? _localization;

    public CharacterCommand(ICharacterService characters, CharactersConfig config, ILocalizationService? localization = null)
    {
        _localization = localization;
        _characters = characters;
        _config = config;
    }

    public async Task ExecuteAsync(CommandContext context)
    {
        var arguments = context.Arguments;

        switch (arguments)
        {
            case ["pending"]:
                await PendingAsync(context, null);

                return;
            case ["pending", var account] when Serial.TryParse(account, out var accountId):
                await PendingAsync(context, accountId);

                return;
            case ["restore", var character] when Serial.TryParse(character, out var characterId):
                await RestoreAsync(context, characterId);

                return;
            default:
                context.PrintError(_localization.Text(CommandMessages.Usage, "Usage: {0}", Syntax));

                return;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetArgumentCompletions(IReadOnlyList<string> previousArguments)
    {
        return previousArguments.Count == 0 ? ["pending", "restore"] : [];
    }

    private async Task PendingAsync(CommandContext context, Serial? accountId)
    {
        var pending = await _characters.GetPendingDeletionsAsync(accountId, context.CancellationToken);

        if (pending.Count == 0)
        {
            context.Print(_localization.Text(CommandMessages.NoPendingDeletions, "No characters are pending deletion."));

            return;
        }

        foreach (var character in pending)
        {
            var requested = character.DeletionRequestedAt!.Value;
            var removable = requested.AddHours(_config.DeletionDelayHours);
            context.Print(
                _localization.Text(
                    CommandMessages.PendingDeletion,
                    "{0} \"{1}\" account {2}: requested {3} UTC, removable after {4} UTC",
                    character.Id,
                    character.DisplayName(),
                    character.AccountId!,
                    Format(requested),
                    Format(removable)
                )
            );
        }
    }

    private async Task RestoreAsync(CommandContext context, Serial characterId)
    {
        var restored = await _characters.RestoreAsync(characterId, context.CancellationToken);

        if (restored is null)
        {
            context.PrintError(_localization.Text(CommandMessages.NotPendingDeletion, "No character {0} is pending deletion.", characterId));

            return;
        }

        _logger.Information("Character {Character} restored from pending deletion", restored);
        context.Print(_localization.Text(CommandMessages.CharacterRestored, "Character {0} \"{1}\" restored.", restored.Id, restored.DisplayName()));
    }

    private static string Format(DateTime utc)
    {
        return utc.ToString(TimeFormat, CultureInfo.InvariantCulture);
    }
}
