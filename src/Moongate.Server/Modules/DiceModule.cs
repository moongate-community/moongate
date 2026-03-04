using Moongate.Core.DiceNotation;
using Moongate.Scripting.Attributes.Scripts;

namespace Moongate.Server.Modules;

[ScriptModule("dice", "Provides dice notation helpers for scripts.")]

/// <summary>
/// Exposes dice notation parsing and rolling helpers to Lua scripts.
/// </summary>
public sealed class DiceModule
{
    [ScriptFunction("roll", "Parses and rolls a dice expression (example: 1d4+2).")]
    public int Roll(string expression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);

        return Dice.Roll(expression);
    }

    [ScriptFunction("try_roll", "Attempts to parse/roll an expression and returns (ok, value).")]
    public (bool Ok, int Value) TryRoll(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return (false, 0);
        }

        try
        {
            return (true, Dice.Roll(expression));
        }
        catch
        {
            return (false, 0);
        }
    }
}
