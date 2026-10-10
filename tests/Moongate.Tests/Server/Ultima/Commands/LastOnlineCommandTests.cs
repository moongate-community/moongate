using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Commands;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Localization;
using Moongate.Tests.TestSupport.Persistence;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Commands;

public sealed class LastOnlineCommandTests
{
    private readonly RecordingDataAccess<MobileEntity> _characters = new();
    private readonly MobileService _mobiles = new(new StubMovementService(), TestSectors.Create());

    [Fact]
    public async Task ACharacterNotInTheWorld_ShowsWhenItLeft()
    {
        Add(2, "Aria", new DateTime(2026, 10, 9, 18, 5, 0, DateTimeKind.Utc));

        var context = await RunAsync("aria");

        Assert.Equal("Aria was last online on 2026-10-09 18:05 (UTC).", Assert.Single(context.Output).Text);
    }

    [Fact]
    public async Task TheDate_IsWrittenTheSameWhateverTheCultureOfTheHost()
    {
        Add(2, "Aria", new DateTime(2026, 10, 9, 18, 5, 0, DateTimeKind.Utc));
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = new("th-TH");

        try
        {
            var context = await RunAsync("aria");

            Assert.Equal("Aria was last online on 2026-10-09 18:05 (UTC).", Assert.Single(context.Output).Text);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public async Task ACharacterInTheWorld_IsOnline()
    {
        Add(2, "Aria", new DateTime(2026, 10, 9, 18, 5, 0, DateTimeKind.Utc));
        _mobiles.EnterWorld(
            new MobileEntity { Id = new Serial(2), AccountId = new Serial(42), Name = "Aria", Map = MapType.Trammel }
        );

        var context = await RunAsync("Aria");

        Assert.Equal("Aria is online now.", Assert.Single(context.Output).Text);
    }

    [Fact]
    public async Task ACharacterThatNeverLeftSinceTheDateWasRecorded_SaysSo()
    {
        Add(2, "Aria", null);

        var context = await RunAsync("Aria");

        Assert.StartsWith("Aria has no last online date yet", Assert.Single(context.Output).Text);
    }

    [Fact]
    public async Task ANameOfSeveralWords_IsJoined()
    {
        Add(2, "Aria Stark", new DateTime(2026, 1, 2, 3, 4, 0, DateTimeKind.Utc));

        var context = await RunAsync("Aria", "Stark");

        Assert.Equal("Aria Stark was last online on 2026-01-02 03:04 (UTC).", Assert.Single(context.Output).Text);
    }

    [Fact]
    public async Task SeveralCharactersOfOneName_AreAllListed()
    {
        Add(2, "Aria", new DateTime(2026, 1, 2, 3, 4, 0, DateTimeKind.Utc));
        Add(3, "Aria", new DateTime(2026, 2, 3, 4, 5, 0, DateTimeKind.Utc));

        var context = await RunAsync("Aria");

        Assert.Equal(2, context.Output.Count);
    }

    [Fact]
    public async Task NobodyOfThatName_IsAnError()
    {
        var context = await RunAsync("Nobody");

        var line = Assert.Single(context.Output);
        Assert.Equal(CommandOutputLevel.Error, line.Level);
        Assert.Equal("No character is named Nobody.", line.Text);
    }

    [Fact]
    public async Task WithoutAName_ShowsTheUsage()
    {
        var context = await RunAsync();

        Assert.Equal(
            (CommandOutputLevel.Error, "Usage: lastonline <name>"),
            (Assert.Single(context.Output).Level, context.Output[0].Text)
        );
    }

    [Fact]
    public async Task ACharacterPendingDeletion_IsLeftOut()
    {
        Add(2, "Aria", null).DeletionRequestedAt = DateTime.UtcNow;

        var context = await RunAsync("Aria");

        Assert.Equal(CommandOutputLevel.Error, Assert.Single(context.Output).Level);
    }

    [Fact]
    public async Task Texts_AreInTheServerLanguage()
    {
        Add(2, "Aria", new DateTime(2026, 10, 9, 18, 5, 0, DateTimeKind.Utc));

        var context = await RunAsync(["Aria"], TestLocalization.With((30243, "{0} è stato qui il {1}.")));

        Assert.Equal("Aria è stato qui il 2026-10-09 18:05.", Assert.Single(context.Output).Text);
    }

    private MobileEntity Add(uint id, string name, DateTime? lastOnline)
    {
        var character = new MobileEntity
            { Id = new Serial(id), AccountId = new Serial(42), Name = name, LastOnlineAt = lastOnline };
        _characters.Upserted.Add(character);

        return character;
    }

    private Task<CommandContext> RunAsync(params string[] arguments)
    {
        return RunAsync(arguments, null);
    }

    private async Task<CommandContext> RunAsync(
        string[] arguments, Moongate.Server.Core.Interfaces.Services.ILocalizationService? localization
    )
    {
        var context = new CommandContext("lastonline", "lastonline", arguments, CommandSourceType.Console, null);
        await new LastOnlineCommand(_characters, _mobiles, localization).ExecuteAsync(context);

        return context;
    }
}
