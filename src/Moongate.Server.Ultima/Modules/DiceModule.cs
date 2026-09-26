using Moongate.Core.Primitives;
using Moongate.Scripting.Attributes.Scripts;

namespace Moongate.Server.Ultima.Modules;

/// <summary>
///     The <c>dice</c> Lua module: rolls dice notation such as <c>1d4+2</c> or <c>4d6k3</c>, the same forms
///     <see cref="DiceSpec" /> reads from templates.
/// </summary>
[ScriptModule("dice", "Rolls dice notation, such as 1d4+2.")]
public sealed class DiceModule
{
    /// <summary>
    ///     Rolls <paramref name="expression" />; scripts call it as <c>dice.roll("1d4+2")</c>. A malformed expression
    ///     raises a Lua error naming it.
    /// </summary>
    /// <exception cref="FormatException">The expression is not a number or a dice expression.</exception>
    [ScriptFunction(helpText: "Rolls a dice expression such as 1d4+2; a malformed one raises an error.")]
    public int Roll(string expression)
    {
        return DiceSpec.Parse(expression).Roll();
    }

    /// <summary>
    ///     Rolls <paramref name="expression" />, or gives <c>nil</c> when it is malformed; scripts call it as
    ///     <c>dice.try_roll(text) or 0</c>.
    /// </summary>
    [ScriptFunction(helpText: "Rolls a dice expression such as 1d4+2, or returns nil when it is malformed.")]
    public int? TryRoll(string expression)
    {
        return DiceSpec.TryParse(expression, out var spec) ? spec.Roll() : null;
    }
}
