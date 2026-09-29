using System.Globalization;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces;
using Serilog;

namespace Moongate.Server.Ultima.Commands;

/// <summary>
///     Staff operations on player characters: list the ones pending deletion and restore them.
/// </summary>
public sealed class CharacterCommand : ICommandExecutor
{
    private const string Usage = "Usage: character pending [account-serial] | character restore <character-serial>";
    private const string TimeFormat = "yyyy-MM-dd HH:mm";

    private readonly ILogger _logger = Log.ForContext<CharacterCommand>();
    private readonly ICharacterService _characters;
    private readonly CharactersConfig _config;

    public CharacterCommand(ICharacterService characters, CharactersConfig config)
    {
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
                context.PrintError(Usage);

                return;
        }
    }

    private async Task PendingAsync(CommandContext context, Serial? accountId)
    {
        var pending = await _characters.GetPendingDeletionsAsync(accountId, context.CancellationToken);

        if (pending.Count == 0)
        {
            context.Print("No characters are pending deletion.");

            return;
        }

        foreach (var character in pending)
        {
            var requested = character.DeletionRequestedAt!.Value;
            var removable = requested.AddHours(_config.DeletionDelayHours);
            context.Print(
                $"{character.Id} \"{character.DisplayName()}\" account {character.AccountId}: " +
                $"requested {Format(requested)} UTC, removable after {Format(removable)} UTC"
            );
        }
    }

    private async Task RestoreAsync(CommandContext context, Serial characterId)
    {
        var restored = await _characters.RestoreAsync(characterId, context.CancellationToken);

        if (restored is null)
        {
            context.PrintError($"No character {characterId} is pending deletion.");

            return;
        }

        _logger.Information("Character {Character} restored from pending deletion", restored);
        context.Print($"Character {restored.Id} \"{restored.DisplayName()}\" restored.");
    }

    private static string Format(DateTime utc)
    {
        return utc.ToString(TimeFormat, CultureInfo.InvariantCulture);
    }
}
