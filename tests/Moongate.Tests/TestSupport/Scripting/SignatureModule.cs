using Moongate.Scripting.Attributes.Scripts;

namespace Moongate.Tests.TestSupport.Scripting;

/// <summary>
///     A function with a nullable, a numeric default and a bool default, returning a string or nothing, for describing
///     signatures.
/// </summary>
[ScriptModule("signature")]
public sealed class SignatureModule
{
    [ScriptFunction(helpText: "Walks.")]
    public string? WalkTo(long serial, int? z = null, int range = 2, bool running = false, string mode = "walk")
    {
        return running || z is null || range < 0 || mode.Length == 0 ? null : serial.ToString();
    }

    [ScriptFunction(helpText: "Counts twice.")]
    public int Twice(int repeatCount, bool inSight = true)
    {
        return inSight ? repeatCount * 2 : repeatCount;
    }
}
