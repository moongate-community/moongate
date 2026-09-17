using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Commands;

namespace Moongate.Tests.TestSupport.Console;

internal sealed class RecordingPromptService : IConsolePromptService
{
    private readonly List<string> _calls = [];
    private readonly List<CommandOutputLine> _output = [];

    public bool IsInteractive { get; set; } = true;

    public bool IsInputLocked { get; private set; } = true;

    public char UnlockCharacter => '*';

    public IReadOnlyList<string> Calls => _calls;

    public IReadOnlyList<CommandOutputLine> Output => _output;

    public string CurrentInput { get; private set; } = "";

    public bool PromptVisible { get; private set; }

    public void RunWithPromptHidden(Action write)
    {
        write();
    }

    public void WriteOutputLine(string text, CommandOutputLevel level)
    {
        lock (_output)
        {
            _output.Add(new CommandOutputLine(text, level));
        }
    }

    public void ShowPrompt()
    {
        PromptVisible = true;
        Record("show");
    }

    public void HidePrompt()
    {
        PromptVisible = false;
        Record("hide");
    }

    public void UpdateInput(string input)
    {
        CurrentInput = input;
        Record($"input:{input}");
    }

    public void LockInput()
    {
        IsInputLocked = true;
        CurrentInput = "";
        Record("lock");
    }

    public void UnlockInput()
    {
        IsInputLocked = false;
        Record("unlock");
    }

    private void Record(string call)
    {
        lock (_calls)
        {
            _calls.Add(call);
        }
    }
}
