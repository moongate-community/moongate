using DryIoc;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Commands;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Services.Commands;
using Moongate.Server.Ultima.Commands;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Tests.TestSupport.Ultima.Characters;

namespace Moongate.Tests.Server.Ultima.Commands;

public sealed class CharacterCommandTests
{
    private static readonly DateTime Requested = new(2026, 9, 28, 10, 30, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Pending_ListsEachCharacterWithWhenItCanBeRemoved()
    {
        var characters = new RecordingCharacterService { Characters = [Pending(3, "Bran", 0x2A)] };

        var output = await ExecuteAsync("character pending", characters);

        Assert.Equal(
            "0x00000003 \"Bran\" account 0x0000002A: requested 2026-09-28 10:30 UTC, removable after 2026-09-29 10:30 UTC",
            Assert.Single(output).Text
        );
    }

    [Fact]
    public async Task Pending_NoneWaiting_SaysSo()
    {
        var output = await ExecuteAsync("character pending", new RecordingCharacterService());

        Assert.Equal("No characters are pending deletion.", Assert.Single(output).Text);
    }

    [Fact]
    public async Task Restore_PendingCharacter_PrintsItsName()
    {
        var characters = new RecordingCharacterService { Characters = [Pending(3, "Bran", 0x2A)] };

        var output = await ExecuteAsync("character restore 0x00000003", characters);

        Assert.Equal("Character 0x00000003 \"Bran\" restored.", Assert.Single(output).Text);
    }

    [Fact]
    public async Task Restore_NotPending_IsAnError()
    {
        var output = await ExecuteAsync("character restore 0x00000009", new RecordingCharacterService());

        var line = Assert.Single(output);
        Assert.Equal(CommandOutputLevel.Error, line.Level);
        Assert.Equal("No character 0x00000009 is pending deletion.", line.Text);
    }

    [Theory, InlineData("character"), InlineData("character restore"), InlineData("character restore nope"), InlineData("character wipe")]
    public async Task BadUsage_PrintsTheUsage(string commandLine)
    {
        var output = await ExecuteAsync(commandLine, new RecordingCharacterService());

        Assert.StartsWith("Usage: character", Assert.Single(output).Text, StringComparison.Ordinal);
    }

    private static MobileEntity Pending(uint serial, string name, uint account)
    {
        return new() { Id = new(serial), Name = name, AccountId = new Serial(account), DeletionRequestedAt = Requested };
    }

    private static async Task<IReadOnlyList<CommandOutputLine>> ExecuteAsync(
        string commandLine,
        RecordingCharacterService characters
    )
    {
        using var container = new Container();
        container.RegisterInstance<ICharacterService>(characters);
        container.RegisterInstance(new CharactersConfig());
        container.RegisterCommand<CharacterCommand>(
            "character",
            source: CommandSourceType.Console | CommandSourceType.InGame,
            minimumAccountType: AccountType.GameMaster
        );
        var commands = new CommandSystemService(container.Resolve<CommandRegistry>(), container);
        await commands.StartAsync();

        try
        {
            return await commands.ExecuteAsync(commandLine, CommandSourceType.Console);
        }
        finally
        {
            await commands.StopAsync();
        }
    }
}
