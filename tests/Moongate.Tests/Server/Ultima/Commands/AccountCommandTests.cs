using DryIoc;
using Moongate.Core.Directories;
using Moongate.Persistence.Extensions;
using Moongate.Server.Core.Commands;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Services.Commands;
using Moongate.Server.Ultima;
using Moongate.Server.Ultima.Commands;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types;
using Moongate.Tests.TestSupport.Persistence;
using Moongate.Tests.TestSupport.Server.Ultima;

namespace Moongate.Tests.Server.Ultima.Commands;

public sealed class AccountCommandTests
{
    [Fact]
    public void UltimaPlugin_RegistersAccountCommandForConsoleAndInGameAdministrator()
    {
        using var directory = new TemporaryPersistenceDirectory();
        using var container = new Container();
        container.RegisterInstance(new DirectoriesConfig(directory.Path, []));
        container.RegisterMoongatePersistence(new());

        new MoongateUltimaPlugin().Register(container);

        var definition = container.Resolve<CommandRegistry>().Registrations["account"].Definition;
        Assert.Equal(typeof(AccountCommand), definition.ExecutorType);
        Assert.Equal(CommandSourceType.Console | CommandSourceType.InGame, definition.Source);
        Assert.Equal(AccountType.Administrator, definition.MinimumAccountType);
    }

    [Fact]
    public async Task Create_WithoutLevel_CreatesRegularAccountWithoutPrintingPassword()
    {
        var accounts = new RecordingAccountService();

        var output = await ExecuteAsync("account create alice synthetic-password", accounts);

        Assert.Equal("alice", accounts.Username);
        Assert.Equal("synthetic-password", accounts.Password);
        Assert.Equal(AccountType.Regular, accounts.RequestedAccountType);
        Assert.Equal("Account 'alice' created (Regular).", Assert.Single(output).Text);
        Assert.DoesNotContain("synthetic-password", output[0].Text, StringComparison.Ordinal);
    }

    [Theory, InlineData("GameMaster", AccountType.GameMaster), InlineData("administrator", AccountType.Administrator)]
    public async Task Create_WithLevel_PassesRequestedAccountType(string level, AccountType expected)
    {
        var accounts = new RecordingAccountService();

        var output = await ExecuteAsync($"account create alice synthetic-password {level}", accounts);

        Assert.Equal(expected, accounts.RequestedAccountType);
        Assert.Equal(CommandOutputLevel.Information, Assert.Single(output).Level);
    }

    [Theory, InlineData("account"), InlineData("account create alice"),
     InlineData("account create alice synthetic-password bogus"),
     InlineData("account remove alice synthetic-password")]
    public async Task Create_WithInvalidArguments_DoesNotCallAccountService(string commandLine)
    {
        var accounts = new RecordingAccountService();

        var output = await ExecuteAsync(commandLine, accounts);

        Assert.Equal(0, accounts.CreateCount);
        Assert.Equal(CommandOutputLevel.Error, Assert.Single(output).Level);
        Assert.DoesNotContain("synthetic-password", output[0].Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Create_WhenUsernameExists_PrintsSafeError()
    {
        var accounts = new RecordingAccountService
        {
            Result = new(false, AccountCreateResultType.UsernameAlreadyExists)
        };

        var output = await ExecuteAsync("account create alice synthetic-password", accounts);

        Assert.Equal("Account 'alice' already exists.", Assert.Single(output).Text);
        Assert.Equal(CommandOutputLevel.Error, output[0].Level);
    }

    [Fact]
    public async Task Create_WhenStorageFails_DoesNotPrintExceptionOrPassword()
    {
        var accounts = new RecordingAccountService
        {
            Result = new(
                false,
                AccountCreateResultType.Error,
                exception: new InvalidOperationException("synthetic-password must stay private")
            )
        };

        var output = await ExecuteAsync("account create alice synthetic-password", accounts);

        Assert.Equal("Account creation failed. Check server logs.", Assert.Single(output).Text);
        Assert.Equal(CommandOutputLevel.Error, output[0].Level);
    }

    [Fact]
    public async Task Create_FromInGameWithoutAdminSession_IsRejectedBeforeAccountService()
    {
        var accounts = new RecordingAccountService();

        var output = await ExecuteAsync("account create alice synthetic-password", accounts, CommandSourceType.InGame);

        Assert.Equal(0, accounts.CreateCount);
        Assert.Equal(CommandOutputLevel.Error, Assert.Single(output).Level);
    }

    private static async Task<IReadOnlyList<CommandOutputLine>> ExecuteAsync(
        string commandLine,
        RecordingAccountService accounts,
        CommandSourceType source = CommandSourceType.Console
    )
    {
        using var container = new Container();
        container.RegisterInstance<IAccountService>(accounts);
        container.RegisterCommand<AccountCommand>(
            "account",
            source: CommandSourceType.Console | CommandSourceType.InGame,
            minimumAccountType: AccountType.Administrator
        );
        var commands = new CommandSystemService(container.Resolve<CommandRegistry>(), container);
        await commands.StartAsync();

        try
        {
            return await commands.ExecuteAsync(commandLine, source);
        }
        finally
        {
            await commands.StopAsync();
        }
    }
}
