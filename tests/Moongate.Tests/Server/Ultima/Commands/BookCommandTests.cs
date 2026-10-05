using DryIoc;
using Moongate.Server.Core.Commands;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Ultima.Commands;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Services.Books;
using Moongate.Tests.TestSupport.Localization;
using Moongate.Tests.TestSupport.Ultima.Books;

namespace Moongate.Tests.Server.Ultima.Commands;

public sealed class BookCommandTests
{
    [Fact]
    public async Task ExecuteAsync_ValidTemplate_SavesPersonalizedTextInCallerBackpack()
    {
        await using var fixture = await BookTestFixture.CreateAsync();

        var context = await RunAsync(fixture, ["welcome_letter", "contact_name=Vega=North"]);

        var letter = Assert.Single(fixture.Items.GetContents(fixture.Backpack.Id));
        Assert.Equal("readable_scroll", letter.TemplateId);
        Assert.Equal("Welcome Pippo", letter.Name);
        Assert.Equal("Dear Pippo,\n\nBring this to Vega=North.", letter.GetProp<string>("book.content"));
        Assert.Equal("Document welcome_letter is in your backpack.", Assert.Single(context.Output).Text);
        Assert.Empty(fixture.Items.GetContents(fixture.Other.Id));
    }

    [Fact]
    public async Task ExecuteAsync_NoCustomVariables_CreatesDocumentWithoutAssignments()
    {
        await using var fixture = await BookTestFixture.CreateAsync();
        fixture.Source.Variables = [];
        fixture.Source.Content = "Dear $player_name";

        await RunAsync(fixture, ["welcome_letter"]);

        Assert.Equal(
            "Dear Pippo",
            Assert.Single(fixture.Items.GetContents(fixture.Backpack.Id)).GetProp<string>("book.content")
        );
    }

    [Theory]
    [InlineData]
    [InlineData("welcome_letter", "contact_name")]
    [InlineData("welcome_letter", "=Vega")]
    [InlineData("welcome_letter", "contact_name=Vega", "contact_name=Other")]
    public async Task ExecuteAsync_MalformedArguments_ShowsUsageWithoutCreatingItems(params string[] arguments)
    {
        await using var fixture = await BookTestFixture.CreateAsync();

        var context = await RunAsync(fixture, arguments);

        Assert.Equal("Usage: book <template> [name=value ...]", Assert.Single(context.Output).Text);
        Assert.Empty(fixture.Items.GetContents(fixture.Backpack.Id));
        Assert.Single(fixture.Serials.Serials);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownTemplate_ReportsMissingSource()
    {
        await using var fixture = await BookTestFixture.CreateAsync();

        var context = await RunAsync(fixture, ["Welcome_Letter", "contact_name=Vega"]);

        Assert.Equal("No document Welcome_Letter in templates/books.", Assert.Single(context.Output).Text);
        Assert.Empty(fixture.Items.GetContents(fixture.Backpack.Id));
        Assert.Single(fixture.Serials.Serials);
    }

    [Theory]
    [InlineData("welcome_letter")]
    [InlineData("welcome_letter", "other=Vega")]
    [InlineData("welcome_letter", "contact_name=Vega", "player_name=Other")]
    public async Task ExecuteAsync_InvalidTemplateValues_CreatesNothing(params string[] arguments)
    {
        await using var fixture = await BookTestFixture.CreateAsync();

        var context = await RunAsync(fixture, arguments);

        Assert.Equal(
            "Document welcome_letter could not be created. Check its values and backpack availability.",
            Assert.Single(context.Output).Text
        );
        Assert.Empty(fixture.Items.GetContents(fixture.Backpack.Id));
        Assert.Single(fixture.Serials.Serials);
    }

    [Fact]
    public async Task ExecuteAsync_NoBackpack_DoesNotConsumeSerial()
    {
        await using var fixture = await BookTestFixture.CreateAsync();
        await fixture.OnLoopAsync(() => fixture.Items.Remove([fixture.Backpack.Id]));

        var context = await RunAsync(fixture, ["welcome_letter", "contact_name=Vega"]);

        Assert.Contains("could not be created", Assert.Single(context.Output).Text);
        Assert.Single(fixture.Serials.Serials);
    }

    [Fact]
    public async Task ExecuteAsync_RemovedSession_CreatesNothing()
    {
        await using var fixture = await BookTestFixture.CreateAsync();
        fixture.World.Sessions.Remove(fixture.Session.SessionId);

        var context = await RunAsync(fixture, ["welcome_letter", "contact_name=Vega"]);

        Assert.Contains("could not be created", Assert.Single(context.Output).Text);
        Assert.Empty(fixture.Items.GetContents(fixture.Backpack.Id));
        Assert.Single(fixture.Serials.Serials);
    }

    [Fact]
    public async Task ExecuteAsync_CharacterChangesWhileQueued_DoesNotDeliverToEitherCharacter()
    {
        await using var fixture = await BookTestFixture.CreateAsync();
        var context = new CommandContext(
            ".book",
            "book",
            ["welcome_letter", "contact_name=Vega"],
            CommandSourceType.InGame,
            fixture.Session
        );
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var blocker = fixture.OnLoopAsync(() =>
            {
                entered.TrySetResult();
                if (!release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Test did not release the loop.");
                fixture.Session.Set(SessionKeys.CharacterId, fixture.Other.Id);
            }
        );
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task pending;
        try
        {
            pending = Command(fixture).ExecuteAsync(context);
        }
        finally
        {
            release.Set();
        }

        await blocker;
        await pending;

        Assert.Contains("could not be created", Assert.Single(context.Output).Text);
        Assert.Empty(fixture.Items.GetContents(fixture.Backpack.Id));
        Assert.Single(fixture.Serials.Serials);
    }

    [Fact]
    public async Task ExecuteAsync_CanceledWhileQueued_CreatesNothing()
    {
        await using var fixture = await BookTestFixture.CreateAsync();
        using var cancellation = new CancellationTokenSource();
        var context = new CommandContext(
            ".book",
            "book",
            ["welcome_letter", "contact_name=Vega"],
            CommandSourceType.InGame,
            fixture.Session,
            cancellation.Token
        );
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var blocker = fixture.OnLoopAsync(() =>
            {
                entered.TrySetResult();
                if (!release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Test did not release the loop.");
            }
        );
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task pending;
        try
        {
            pending = Command(fixture).ExecuteAsync(context);
            cancellation.Cancel();
        }
        finally
        {
            release.Set();
        }

        await blocker;
        await pending;

        Assert.Empty(fixture.Items.GetContents(fixture.Backpack.Id));
        Assert.Single(fixture.Serials.Serials);
    }

    [Fact]
    public async Task ExecuteAsync_ReservedBackpack_CreatesNothing()
    {
        await using var fixture = await BookTestFixture.CreateAsync();
        await fixture.OnLoopAsync(() => fixture.Reservations.TryReserve(fixture.Player.Id, Task.CompletedTask));

        var context = await RunAsync(fixture, ["welcome_letter", "contact_name=Vega"]);

        Assert.Contains("could not be created", Assert.Single(context.Output).Text);
        Assert.Empty(fixture.Items.GetContents(fixture.Backpack.Id));
        Assert.Single(fixture.Serials.Serials);
    }

    [Fact]
    public async Task ExecuteAsync_Console_RefusesDelivery()
    {
        await using var fixture = await BookTestFixture.CreateAsync();
        var context = new CommandContext("book welcome_letter", "book", ["welcome_letter"], CommandSourceType.Console, null);

        await Command(fixture).ExecuteAsync(context);

        Assert.Equal("book works in game only.", Assert.Single(context.Output).Text);
        Assert.Single(fixture.Serials.Serials);
    }

    [Fact]
    public async Task ExecuteAsync_Localization_UsesConfiguredMessage()
    {
        await using var fixture = await BookTestFixture.CreateAsync();

        var context = await RunAsync(
            fixture,
            ["welcome_letter", "contact_name=Vega"],
            TestLocalization.With((30182, "Documento {0} nello zaino."))
        );

        Assert.Equal("Documento welcome_letter nello zaino.", Assert.Single(context.Output).Text);
    }

    [Fact]
    public void AddUltimaCommands_Book_IsRestrictedToInGameGameMasters()
    {
        using var container = new Container();

        container.AddUltimaCommands();

        var definition = container.Resolve<CommandRegistry>().Registrations["book"].Definition;
        Assert.Equal(CommandSourceType.InGame, definition.Source);
        Assert.Equal(AccountType.GameMaster, definition.MinimumAccountType);
        Assert.Equal(typeof(BookCommand), definition.ExecutorType);
        Assert.Equal(CommandMessages.BookDescription, definition.DescriptionMessage);
    }

    private static BookCommand Command(BookTestFixture fixture, ILocalizationService? localization = null)
    {
        return new(
            fixture.Books,
            new BookTemplateService(fixture.Data),
            fixture.World.Mobiles,
            fixture.World.Sessions,
            fixture.World.Network.Loop,
            localization
        );
    }

    private static async Task<CommandContext> RunAsync(
        BookTestFixture fixture, string[] arguments, ILocalizationService? localization = null
    )
    {
        var context = new CommandContext(".book", "book", arguments, CommandSourceType.InGame, fixture.Session);
        await Command(fixture, localization).ExecuteAsync(context);
        return context;
    }
}
