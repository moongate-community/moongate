using DryIoc;
using Moongate.Server.Core.Commands;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Services.Commands;
using Moongate.Server.Services.Console;
using Moongate.Server.Services.Logging;
using Moongate.Tests.TestSupport.Commands;
using Moongate.Tests.TestSupport.Console;

namespace Moongate.Tests.Integration.Server.Services.Logging;

/// <summary>
///     The server's logger end to end: a console command that throws, typed at the console as an operator does.
/// </summary>
public sealed class ServerLoggerFactoryTests : IDisposable
{
    private readonly string _logs = Path.Combine(Path.GetTempPath(), "moongate-logs-" + Guid.NewGuid().ToString("N"));
    private readonly StringWriter _console = new();

    [Fact]
    public async Task ACommandThatThrows_ShowsOneLineOnTheConsole_AndLeavesAReportAndTheFullLog()
    {
        var prompt = new RecordingPromptService();
        var logger = ServerLoggerFactory.Create(prompt, _logs, true, "0.11.0", "Lilly", _console);
        using var container = new Container();
        container.RegisterCommand<ThrowingCommandExecutor>("boom", minimumAccountType: AccountType.Regular);
        var commands = new CommandSystemService(
            container.Resolve<CommandRegistry>(),
            container,
            logger.ForContext<CommandSystemService>()
        );
        await commands.StartAsync();
        var keys = new ScriptedConsoleKeySource();
        keys.Enqueue('*');
        keys.EnqueueText("boom");
        keys.Enqueue(ConsoleKey.Enter);

        using (var input = new ConsoleInputService(prompt, commands, keys))
        {
            await input.StartAsync();
            await WaitForAsync(() => prompt.Output.Count > 0);
            await input.StopAsync();
        }

        await commands.StopAsync();
        await logger.DisposeAsync();

        // The operator is told the command failed.
        Assert.Equal("The command 'boom' failed.", Assert.Single(prompt.Output).Text);

        // The console: one line with the message and the report, no stack.
        var console = _console.ToString();
        var report = Assert.Single(Directory.GetFiles(Path.Combine(_logs, "errors"), "*.md"));
        Assert.Contains(
            $"Command 'boom' execution failed: command executor failure - details: {report} (paste it into a GitHub issue)",
            console,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain(" at ", console, StringComparison.Ordinal);

        // The report: the whole exception, down to the command that threw.
        var text = await File.ReadAllTextAsync(report);
        Assert.Contains($"System.InvalidOperationException: command executor failure", text, StringComparison.Ordinal);
        Assert.Contains(
            $"{nameof(ThrowingCommandExecutor)}.{nameof(ThrowingCommandExecutor.ExecuteAsync)}",
            text,
            StringComparison.Ordinal
        );
        Assert.Contains("0.11.0 \"Lilly\"", text, StringComparison.Ordinal);

        // The .clef log keeps the full exception and points at the report.
        var clef = await File.ReadAllTextAsync(Assert.Single(Directory.GetFiles(_logs, "moongate-*.clef")));
        Assert.Contains(nameof(ThrowingCommandExecutor.ExecuteAsync), clef, StringComparison.Ordinal);
        Assert.Contains("\"ReportFile\"", clef, StringComparison.Ordinal);
    }

    [Fact]
    public void WithoutFileLogging_TheConsoleStillShowsTheMessage_AndNoReportIsWritten()
    {
        var logger = ServerLoggerFactory.Create(new RecordingPromptService(), _logs, false, "0.11.0", "Lilly", _console);

        logger.Error(new InvalidOperationException("no files please"), "Something failed");
        logger.Dispose();

        Assert.Contains("Something failed: no files please", _console.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("details:", _console.ToString(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(_logs));
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);

        while (DateTime.UtcNow < deadline && !condition())
        {
            await Task.Delay(10);
        }

        Assert.True(condition(), "The condition was not met within the timeout.");
    }

    public void Dispose()
    {
        _console.Dispose();

        if (Directory.Exists(_logs))
        {
            Directory.Delete(_logs, true);
        }
    }
}
