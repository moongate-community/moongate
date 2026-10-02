using Moongate.Server.Commands;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Commands;
using Moongate.Tests.TestSupport.Localization;
using Moongate.Tests.TestSupport.Persistence;

namespace Moongate.Tests.Server.Commands;

public sealed class SqlBackupCommandTests
{
    private readonly StubSqlBackupService _backups = new();

    [Fact]
    public async Task ExecuteAsync_ListsEachWrittenFileWithItsSize()
    {
        _backups.Result = new()
        {
            Files =
            [
                new() { Database = "auth", Path = "/srv/backups/auth_20261002_113000.sql", Size = 2048 },
                new() { Database = "world", Path = "/srv/backups/world_20261002_113000.sql", Size = 1_258_291 }
            ]
        };

        var context = await RunAsync();

        Assert.Equal(1, _backups.Calls);
        Assert.Equal(
            [
                (CommandOutputLevel.Information, "SQL backup started."),
                (CommandOutputLevel.Information, "auth_20261002_113000.sql (2 KB)"),
                (CommandOutputLevel.Information, "world_20261002_113000.sql (1.2 MB)")
            ],
            context.Output.Select(line => (line.Level, line.Text))
        );
    }

    [Fact]
    public async Task ExecuteAsync_WhenABackupIsRunning_SaysSo()
    {
        _backups.Result = new() { AlreadyRunning = true };

        var context = await RunAsync();

        Assert.Equal(
            (CommandOutputLevel.Warning, "A SQL backup is already running."),
            (Assert.Single(context.Output).Level, context.Output[0].Text)
        );
    }

    [Fact]
    public async Task ExecuteAsync_AFailedDatabase_IsAnErrorLineNextToTheWrittenFiles()
    {
        _backups.Result = new()
        {
            Files = [new() { Database = "auth", Path = "/srv/backups/auth_20261002_113000.sql", Size = 10 }],
            Failures = [new() { Database = "world", Reason = "disk full" }]
        };

        var context = await RunAsync();

        Assert.Equal(
            [
                (CommandOutputLevel.Information, "SQL backup started."),
                (CommandOutputLevel.Information, "auth_20261002_113000.sql (10 B)"),
                (CommandOutputLevel.Error, "SQL backup of world failed: disk full.")
            ],
            context.Output.Select(line => (line.Level, line.Text))
        );
    }

    [Fact]
    public async Task ExecuteAsync_AReasonWithBraces_IsPrintedAsIs()
    {
        _backups.Result = new() { Failures = [new() { Database = "world", Reason = "bad {0} value {x}" }] };

        var context = await RunAsync();

        Assert.Equal("SQL backup of world failed: bad {0} value {x}.", context.Output[^1].Text);
    }

    [Theory, InlineData("now"), InlineData("world", "auth")]
    public async Task ExecuteAsync_WithArguments_ShowsTheUsageAndBacksUpNothing(params string[] arguments)
    {
        var context = await RunAsync(arguments);

        Assert.Equal(0, _backups.Calls);
        Assert.Equal(
            (CommandOutputLevel.Error, "Usage: sql_backup"),
            (Assert.Single(context.Output).Level, context.Output[0].Text)
        );
    }

    [Fact]
    public async Task Texts_AreInTheServerLanguage()
    {
        _backups.Result = new() { AlreadyRunning = true };

        var context = await RunAsync([], TestLocalization.With((30106, "Un backup SQL è già in corso.")));

        Assert.Equal("Un backup SQL è già in corso.", Assert.Single(context.Output).Text);
    }

    [Theory]
    [InlineData(0L, "0 B")]
    [InlineData(1023L, "1023 B")]
    [InlineData(1024L, "1 KB")]
    [InlineData(1536L, "1.5 KB")]
    [InlineData(1_258_291L, "1.2 MB")]
    [InlineData(5_368_709_120L, "5 GB")]
    public void FormatSize_UsesBinaryUnitsAndAnInvariantDecimalPoint(long bytes, string expected)
    {
        Assert.Equal(expected, SqlBackupCommand.FormatSize(bytes));
    }

    private async Task<CommandContext> RunAsync(string[]? arguments = null, ILocalizationService? localization = null)
    {
        arguments ??= [];
        var line = ("sql_backup " + string.Join(' ', arguments)).TrimEnd();
        var context = new CommandContext(line, "sql_backup", arguments, CommandSourceType.Console, null);

        await new SqlBackupCommand(_backups, localization).ExecuteAsync(context);

        return context;
    }
}
