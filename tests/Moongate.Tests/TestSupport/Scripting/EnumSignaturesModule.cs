using Moongate.Scripting.Attributes.Scripts;

namespace Moongate.Tests.TestSupport.Scripting;

/// <summary>
/// Functions whose signatures put an enum in the optional, nullable and vararg positions, for the definitions
/// generator.
/// </summary>
[ScriptModule("palette")]
public sealed class EnumSignaturesModule
{
    [ScriptFunction]
    public int Mix(params ProbeColour[] colours)
        => colours.Length;

    [ScriptFunction]
    public int Paint(ProbeColour? colour)
        => colour.HasValue ? (int)colour.Value : -1;

    [ScriptFunction]
    public int Tint(ProbeColour colour = ProbeColour.Red)
        => (int)colour;
}
