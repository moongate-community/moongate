using DryIoc;
using Moongate.Server.Commands;
using Moongate.Server.Core.Commands;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Services.Commands;
using Moongate.Server.Services.Console;
using Moongate.Tests.TestSupport.Console;
using Moongate.Tests.TestSupport.Commands;

namespace Moongate.Tests.Server.Services.Console;

public sealed class ConsoleInputServiceTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Enter_DispatchesTheTypedLineAndRendersTheOutput()
    {
        var prompt = new RecordingPromptService();
        var keys = new ScriptedConsoleKeySource();
        keys.Enqueue('*');
        keys.EnqueueText("echo hello");
        keys.Enqueue(ConsoleKey.Enter);
        using var container = CreateContainer();
        var commands = await CreateCommandsAsync(container);
        using var service = new ConsoleInputService(prompt, commands, keys);
        await service.StartAsync();

        await WaitForAsync(() => prompt.Output.Count > 0);

        var line = Assert.Single(prompt.Output);
        Assert.Equal("hello", line.Text);
        Assert.Equal(CommandOutputLevel.Information, line.Level);
        await service.StopAsync();
        await commands.StopAsync();
    }

    [Fact]
    public async Task AccountCreate_MasksPasswordOnPromptButDispatchesOriginalValue()
    {
        var prompt = new RecordingPromptService();
        var keys = new ScriptedConsoleKeySource();
        keys.Enqueue('*');
        keys.EnqueueText("account create alice synthetic-password Administrator");
        keys.Enqueue(ConsoleKey.Enter);
        using var container = CreateContainer();
        container.RegisterCommand<RecordingCommandExecutor>("account");
        var commands = await CreateCommandsAsync(container);
        using var service = new ConsoleInputService(prompt, commands, keys);
        await service.StartAsync();

        await WaitForAsync(() => prompt.Output.Count > 0);
        await service.StopAsync();

        const string prefix = "input:account create alice ";
        foreach (var call in prompt.Calls.Where(call => call.StartsWith(prefix, StringComparison.Ordinal)))
        {
            var displayedPassword = call[prefix.Length..].Split(' ')[0];
            Assert.All(displayedPassword, character => Assert.Equal('*', character));
        }

        var invocation = Assert.Single(container.Resolve<RecordingCommandExecutor>().Invocations);
        Assert.Equal("synthetic-password", invocation.Arguments[2]);
        await commands.StopAsync();
    }

    [Fact]
    public async Task Enter_OnBlankLineDoesNotDisturbTheNextCommand()
    {
        var prompt = new RecordingPromptService();
        var keys = new ScriptedConsoleKeySource();
        keys.Enqueue('*');
        keys.Enqueue(ConsoleKey.Enter);
        keys.EnqueueText("echo x");
        keys.Enqueue(ConsoleKey.Enter);
        using var container = CreateContainer();
        var commands = await CreateCommandsAsync(container);
        using var service = new ConsoleInputService(prompt, commands, keys);
        await service.StartAsync();

        await WaitForAsync(() => prompt.Output.Count > 0);

        Assert.Equal("x", Assert.Single(prompt.Output).Text);
        await service.StopAsync();
        await commands.StopAsync();
    }

    [Fact]
    public async Task Enter_UnknownCommandRendersAnErrorLineAndTheLoopSurvives()
    {
        var prompt = new RecordingPromptService();
        var keys = new ScriptedConsoleKeySource();
        keys.Enqueue('*');
        keys.EnqueueText("nope");
        keys.Enqueue(ConsoleKey.Enter);
        using var container = CreateContainer();
        var commands = await CreateCommandsAsync(container);
        using var service = new ConsoleInputService(prompt, commands, keys);
        await service.StartAsync();

        await WaitForAsync(() => prompt.Output.Count > 0);
        Assert.Equal(CommandOutputLevel.Error, prompt.Output[0].Level);

        keys.EnqueueText("echo alive");
        keys.Enqueue(ConsoleKey.Enter);
        await WaitForAsync(() => prompt.Output.Count > 1);
        Assert.Equal("alive", prompt.Output[1].Text);

        await service.StopAsync();
        await commands.StopAsync();
    }

    [Fact]
    public async Task LockedInput_IgnoresKeysUntilTheUnlockCharacter()
    {
        var prompt = new RecordingPromptService();
        var keys = new ScriptedConsoleKeySource();
        keys.EnqueueText("abc");
        keys.Enqueue('*');
        using var container = CreateContainer();
        var commands = await CreateCommandsAsync(container);
        using var service = new ConsoleInputService(prompt, commands, keys);
        await service.StartAsync();

        await WaitForAsync(() => !prompt.IsInputLocked);

        Assert.Equal("", prompt.CurrentInput);
        await service.StopAsync();
        await commands.StopAsync();
    }

    [Fact]
    public async Task RunAsync_KeySourceFailureStopsTheLoopAndHidesThePrompt()
    {
        var prompt = new RecordingPromptService();
        var keys = new ScriptedConsoleKeySource();
        using var container = CreateContainer();
        var commands = await CreateCommandsAsync(container);
        using var service = new ConsoleInputService(prompt, commands, keys);
        await service.StartAsync();

        keys.ThrowOnNextRead = new IOException("console detached");

        await WaitForAsync(() => !prompt.PromptVisible);

        var exception = await Record.ExceptionAsync(() => service.StopAsync());

        Assert.Null(exception);
        Assert.False(prompt.PromptVisible);
        await commands.StopAsync();
    }

    [Fact]
    public async Task StartAsync_LocksInputAndShowsThePrompt()
    {
        var prompt = new RecordingPromptService();
        var keys = new ScriptedConsoleKeySource();
        using var container = CreateContainer();
        var commands = await CreateCommandsAsync(container);
        using var service = new ConsoleInputService(prompt, commands, keys);

        await service.StartAsync();

        Assert.True(prompt.PromptVisible);
        Assert.True(prompt.IsInputLocked);
        await service.StopAsync();
        await commands.StopAsync();
    }

    [Fact]
    public async Task StartAsync_NonInteractiveDoesNotShowThePrompt()
    {
        var prompt = new RecordingPromptService { IsInteractive = false };
        var keys = new ScriptedConsoleKeySource();
        using var container = CreateContainer();
        var commands = await CreateCommandsAsync(container);
        using var service = new ConsoleInputService(prompt, commands, keys);

        await service.StartAsync();

        Assert.False(prompt.PromptVisible);
        await service.StopAsync();
        await commands.StopAsync();
    }

    [Fact]
    public async Task StopAsync_EndsTheLoopAndHidesThePrompt()
    {
        var prompt = new RecordingPromptService();
        var keys = new ScriptedConsoleKeySource();
        using var container = CreateContainer();
        var commands = await CreateCommandsAsync(container);
        using var service = new ConsoleInputService(prompt, commands, keys);
        await service.StartAsync();

        await service.StopAsync();

        Assert.False(prompt.PromptVisible);
        Assert.Contains("hide", prompt.Calls);
        await commands.StopAsync();
    }

    [Fact]
    public async Task UnlockedInput_AccumulatesCharactersAndEditsWithBackspaceAndEscape()
    {
        var prompt = new RecordingPromptService();
        var keys = new ScriptedConsoleKeySource();
        keys.Enqueue('*');
        keys.EnqueueText("echo");
        keys.Enqueue(ConsoleKey.Backspace);
        using var container = CreateContainer();
        var commands = await CreateCommandsAsync(container);
        using var service = new ConsoleInputService(prompt, commands, keys);
        await service.StartAsync();

        await WaitForAsync(() => prompt.CurrentInput == "ech");

        keys.Enqueue(ConsoleKey.Escape);
        await WaitForAsync(() => prompt.CurrentInput == "");

        await service.StopAsync();
        await commands.StopAsync();
    }

    private static async Task<CommandSystemService> CreateCommandsAsync(Container container)
    {
        var service = new CommandSystemService(container.Resolve<CommandRegistry>(), container);
        await service.StartAsync();

        return service;
    }

    private static Container CreateContainer()
    {
        var container = new Container();
        container.RegisterCommand<EchoCommand>("echo", minimumAccountType: AccountType.Regular);

        return container;
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Timeout;

        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(10);
        }

        Assert.Fail("Condition was not met within the timeout.");
    }
}
