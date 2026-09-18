using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Services.Console;
using Moongate.Tests.TestSupport.Console;

namespace Moongate.Tests.Server.Services.Console;

public sealed class ConsolePromptServiceTests
{
    [Fact]
    public void RunWithPromptHidden_NonInteractiveJustRunsTheWrite()
    {
        var driver = new RecordingConsoleDriver();
        var service = new ConsolePromptService(driver, interactive: false);
        var ran = false;

        service.RunWithPromptHidden(() => ran = true);

        Assert.True(ran);
        Assert.Empty(driver.Operations);
        Assert.False(service.IsInteractive);
    }

    [Fact]
    public void RunWithPromptHidden_BeforeShowPromptLeavesTheBottomRowAlone()
    {
        var driver = new RecordingConsoleDriver();
        var service = new ConsolePromptService(driver, interactive: true);
        var ran = false;

        service.RunWithPromptHidden(() => ran = true);

        Assert.True(ran);
        Assert.Empty(driver.Operations);
    }

    [Fact]
    public void RunWithPromptHidden_ErasesThenWritesThenRepaints()
    {
        var driver = new RecordingConsoleDriver { WindowWidth = 20, WindowHeight = 10, BufferHeight = 10 };
        var service = new ConsolePromptService(driver, interactive: true);
        service.ShowPrompt();
        var marker = driver.Operations.Count;

        service.RunWithPromptHidden(() => driver.WriteLine("log line"));

        var after = driver.Operations.Skip(marker).ToArray();
        Assert.Equal("pos:0,9", after[0]);
        Assert.Equal($"write:{new string(' ', 19)}", after[1]);
        Assert.Equal("pos:0,9", after[2]);
        Assert.Equal("writeline:log line", after[3]);
        Assert.Contains("write:MG [LOCKED]> ", after);
    }

    [Fact]
    public void ShowPrompt_DrawsTheLockedPrefixAndParksTheCursorAfterIt()
    {
        var driver = new RecordingConsoleDriver { WindowWidth = 40, WindowHeight = 5, BufferHeight = 5 };
        var service = new ConsolePromptService(driver, interactive: true);

        service.ShowPrompt();

        Assert.Contains("write:MG [LOCKED]> ", driver.Operations);
        Assert.Equal("pos:13,4", driver.Operations[^1]);
        Assert.True(service.IsInputLocked);
    }

    [Fact]
    public void UnlockInput_SwitchesToTheUnlockedPrefix()
    {
        var driver = new RecordingConsoleDriver { WindowWidth = 40, WindowHeight = 5, BufferHeight = 5 };
        var service = new ConsolePromptService(driver, interactive: true);
        service.ShowPrompt();

        service.UnlockInput();

        Assert.False(service.IsInputLocked);
        Assert.Contains("write:MG> ", driver.Operations);
    }

    [Fact]
    public void UpdateInput_RendersPrefixPlusBufferAndMovesTheCursor()
    {
        var driver = new RecordingConsoleDriver { WindowWidth = 40, WindowHeight = 5, BufferHeight = 5 };
        var service = new ConsolePromptService(driver, interactive: true);
        service.ShowPrompt();
        service.UnlockInput();

        service.UpdateInput("echo hi");

        Assert.Contains("write:MG> echo hi", driver.Operations);
        Assert.Equal("pos:11,4", driver.Operations[^1]);
    }

    [Fact]
    public void UpdateInput_TruncatesToTheWindowWidth()
    {
        var driver = new RecordingConsoleDriver { WindowWidth = 8, WindowHeight = 5, BufferHeight = 5 };
        var service = new ConsolePromptService(driver, interactive: true);
        service.ShowPrompt();
        service.UnlockInput();

        service.UpdateInput("0123456789");

        Assert.Contains("write:MG> 012", driver.Operations);
        Assert.Equal("pos:7,4", driver.Operations[^1]);
    }

    [Fact]
    public void PromptRow_IsClampedInsideTheBuffer()
    {
        var driver = new RecordingConsoleDriver { WindowWidth = 20, WindowHeight = 10, WindowTop = 95, BufferHeight = 100 };
        var service = new ConsolePromptService(driver, interactive: true);

        service.ShowPrompt();

        Assert.Equal("pos:0,99", driver.Operations[0]);
    }

    [Fact]
    public void LockInput_ClearsThePendingText()
    {
        var driver = new RecordingConsoleDriver { WindowWidth = 40, WindowHeight = 5, BufferHeight = 5 };
        var service = new ConsolePromptService(driver, interactive: true);
        service.ShowPrompt();
        service.UnlockInput();
        service.UpdateInput("half typed");

        service.LockInput();

        Assert.True(service.IsInputLocked);
        Assert.Contains("write:MG [LOCKED]> ", driver.Operations);
        Assert.DoesNotContain("write:MG [LOCKED]> half typed", driver.Operations);
    }

    [Fact]
    public void HidePrompt_ClearsTheRowAndStopsRepainting()
    {
        var driver = new RecordingConsoleDriver { WindowWidth = 20, WindowHeight = 10, BufferHeight = 10 };
        var service = new ConsolePromptService(driver, interactive: true);
        service.ShowPrompt();

        service.HidePrompt();
        var marker = driver.Operations.Count;
        service.RunWithPromptHidden(() => driver.WriteLine("after hide"));

        Assert.Equal(new[] { "writeline:after hide" }, driver.Operations.Skip(marker));
    }

    [Fact]
    public void WriteOutputLine_ColoursErrorsAndResetsAfterwards()
    {
        var driver = new RecordingConsoleDriver { WindowWidth = 40, WindowHeight = 5, BufferHeight = 5 };
        var service = new ConsolePromptService(driver, interactive: true);
        service.ShowPrompt();
        var marker = driver.Operations.Count;

        service.WriteOutputLine("it broke", CommandOutputLevel.Error);

        var after = driver.Operations.Skip(marker).ToArray();
        Assert.Contains("color:Red", after);
        Assert.Contains("writeline:it broke", after);
        Assert.Contains("reset", after);
        Assert.True(Array.IndexOf(after, "color:Red") < Array.IndexOf(after, "writeline:it broke"));
    }

    [Fact]
    public void WriteOutputLine_ColoursWarningsYellow()
    {
        var driver = new RecordingConsoleDriver { WindowWidth = 40, WindowHeight = 5, BufferHeight = 5 };
        var service = new ConsolePromptService(driver, interactive: true);
        service.ShowPrompt();
        var marker = driver.Operations.Count;

        service.WriteOutputLine("careful", CommandOutputLevel.Warning);

        Assert.Contains("color:Yellow", driver.Operations.Skip(marker));
    }

    [Fact]
    public void WriteOutputLine_LeavesInformationUncoloured()
    {
        var driver = new RecordingConsoleDriver { WindowWidth = 40, WindowHeight = 5, BufferHeight = 5 };
        var service = new ConsolePromptService(driver, interactive: true);
        service.ShowPrompt();
        var marker = driver.Operations.Count;

        service.WriteOutputLine("all good", CommandOutputLevel.Information);

        var after = driver.Operations.Skip(marker).ToArray();
        Assert.Contains("writeline:all good", after);
        Assert.DoesNotContain("reset", after);
        Assert.DoesNotContain(after, operation => operation.StartsWith("color:", StringComparison.Ordinal));
    }

    [Fact]
    public void TerminalFailure_DisablesInteractivityPermanentlyAndStillWrites()
    {
        var driver = new RecordingConsoleDriver { WindowWidth = 20, WindowHeight = 10, BufferHeight = 10 };
        var service = new ConsolePromptService(driver, interactive: true);
        service.ShowPrompt();
        driver.ThrowOnOperation = driver.Operations.Count;
        var writes = 0;

        service.RunWithPromptHidden(() => writes++);

        Assert.False(service.IsInteractive);
        Assert.Equal(1, writes);

        driver.ThrowOnOperation = -1;
        service.RunWithPromptHidden(() => writes++);
        Assert.Equal(2, writes);
    }

    [Fact]
    public void RunWithPromptHidden_WriteItselfThrowingDoesNotRunItTwice()
    {
        var driver = new RecordingConsoleDriver { WindowWidth = 20, WindowHeight = 10, BufferHeight = 10 };
        var service = new ConsolePromptService(driver, interactive: true);
        service.ShowPrompt();
        driver.ThrowOnOperation = driver.Operations.Count + 3;
        var writes = 0;

        var exception = Record.Exception(
            () => service.RunWithPromptHidden(
                () =>
                {
                    writes++;
                    driver.Write("callback write");
                }
            )
        );

        Assert.Null(exception);
        Assert.Equal(1, writes);
        Assert.False(service.IsInteractive);
    }
}
