using Moongate.Scripting.Attributes.Scripts;

namespace Moongate.Tests.TestSupport.Scripting;

/// <summary>
///     Functions for describing defaults and enum order: a float default, an enum default that names no member, and
///     three enums met as a parameter, a return and a constant.
/// </summary>
[ScriptModule("defaults")]
public sealed class DefaultsModule
{
    [ScriptConstant] public static readonly DayOfWeek Start = DayOfWeek.Monday;

    [ScriptFunction]
    public RegistryColour Pick(ProbeColour? colour, params ProbeColour[] more)
    {
        return colour.HasValue || more.Length > 0 ? RegistryColour.Red : RegistryColour.Blue;
    }

    [ScriptFunction]
    public double Scale(float factor = 0.1f, ProbeColour colour = (ProbeColour)7)
    {
        return factor * (int)colour;
    }
}
