using Moongate.Server.Abstractions.Data.World;
using MoonSharp.Interpreter;

namespace Moongate.Server.Scripting.Refs;

/// <summary>
/// Turns a target answer into the table the Lua callback receives.
/// <para>
/// A separate type from the module because it needs the MoonSharp <see cref="Script" />, which is
/// not a container service: a script module that asks for one compiles, passes every test, and
/// fails only when the real server tries to construct it.
/// </para>
/// </summary>
public sealed class TargetResultFactory
{
    private readonly Script _script;

    public TargetResultFactory(Script script)
    {
        _script = script;
    }

    /// <summary>
    /// The answer as Lua sees it: <c>cancelled</c>, <c>type</c>, <c>serial</c>, <c>x</c>, <c>y</c>,
    /// <c>z</c> and <c>graphic</c>. Cancellation is a named field rather than a serial of zero,
    /// because it is the answer a player gives most often.
    /// </summary>
    public DynValue ToTable(TargetResult result)
    {
        var table = new Table(_script)
        {
            ["cancelled"] = result.IsCancelled,
            ["type"] = result.Type.ToString().ToLowerInvariant(),
            ["serial"] = result.Serial.Value,
            ["x"] = result.Location.X,
            ["y"] = result.Location.Y,
            ["z"] = result.Location.Z,
            ["graphic"] = result.Graphic
        };

        return DynValue.NewTable(table);
    }
}
