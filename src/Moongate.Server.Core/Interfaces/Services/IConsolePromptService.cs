using Moongate.Server.Core.Types.Commands;

namespace Moongate.Server.Core.Interfaces.Services;

/// <summary>Owns the pinned console prompt row and serialises terminal writes against it.</summary>
public interface IConsolePromptService
{
    /// <summary>Gets whether the process is attached to a terminal that can host the prompt.</summary>
    bool IsInteractive { get; }

    /// <summary>Gets whether keystrokes are currently ignored.</summary>
    bool IsInputLocked { get; }

    /// <summary>Gets the character that releases the input lock.</summary>
    char UnlockCharacter { get; }

    /// <summary>Runs a terminal write with the prompt row temporarily removed.</summary>
    /// <param name="write">The write to perform while the prompt is hidden.</param>
    void RunWithPromptHidden(Action write);

    /// <summary>Writes one command output line above the prompt, coloured by level.</summary>
    /// <param name="text">Line text.</param>
    /// <param name="level">Severity used to pick the colour.</param>
    void WriteOutputLine(string text, CommandOutputLevel level);

    /// <summary>Starts drawing the prompt row.</summary>
    void ShowPrompt();

    /// <summary>Clears the prompt row and stops drawing it.</summary>
    void HidePrompt();

    /// <summary>Replaces the text shown after the prompt prefix.</summary>
    /// <param name="input">Current input buffer.</param>
    void UpdateInput(string input);

    /// <summary>Locks input and clears the pending text.</summary>
    void LockInput();

    /// <summary>Releases the input lock.</summary>
    void UnlockInput();
}
