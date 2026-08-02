using Moongate.Core.Primitives;
using Moongate.Server.Abstractions.Data.World;
using Moongate.Server.Abstractions.Types.World;
using Moongate.Server.Scripting.Refs;
using MoonSharp.Interpreter;

namespace Moongate.Tests.Scripting;

/// <summary>
/// The result table as Lua actually sees it. Cancelled is the case a script writer meets most
/// often, so it has to be readable without knowing a sentinel.
/// </summary>
public class TargetLuaTests
{
    // Readable as `r.cancelled` rather than by comparing a serial to zero, which is what a script
    // writer would otherwise have to guess.
    [Fact]
    public void CancelledResult_SaysSoDirectly()
    {
        var script = new Script();

        script.Globals["r"] = new TargetResultFactory(script).ToTable(TargetResult.Cancelled);

        Assert.True(script.DoString("return r.cancelled").Boolean);
    }

    [Fact]
    public void LocationResult_HasNoSerial()
    {
        var script = new Script();

        script.Globals["r"] = new TargetResultFactory(script)
            .ToTable(new(TargetResultType.Location, Serial.Zero, new(40, 50, 0), 0));

        Assert.Equal(0d, script.DoString("return r.serial").Number);
        Assert.Equal(40d, script.DoString("return r.x").Number);
    }

    [Fact]
    public void ObjectResult_CarriesSerialAndPosition()
    {
        var script = new Script();

        script.Globals["r"] = new TargetResultFactory(script)
            .ToTable(new(TargetResultType.Object, (Serial)0x4000_0001u, new(10, 20, 5), 0x0EED));

        Assert.False(script.DoString("return r.cancelled").Boolean);
        Assert.Equal(0x4000_0001u, script.DoString("return r.serial").Number);
        Assert.Equal(10d, script.DoString("return r.x").Number);
        Assert.Equal(20d, script.DoString("return r.y").Number);
        Assert.Equal(5d, script.DoString("return r.z").Number);
        Assert.Equal(0x0EED, script.DoString("return r.graphic").Number);
    }

    // Also readable as a string, for a script that wants to branch three ways.
    [Fact]
    public void Result_CarriesItsTypeAsALowercaseString()
    {
        var script = new Script();

        script.Globals["r"] = new TargetResultFactory(script)
            .ToTable(new(TargetResultType.Location, Serial.Zero, new(1, 2, 3), 0));

        Assert.Equal("location", script.DoString("return r.type").String);
    }
}
