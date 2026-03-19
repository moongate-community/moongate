using Moongate.Server.Modules;
using MoonSharp.Interpreter;

namespace Moongate.Tests.Server.Modules;

public sealed class ConvertModuleTests
{
    [Test]
    public void ParseDelayMilliseconds_ShouldParseTimeSpanAndNumbers()
    {
        var module = new ConvertModule();

        Assert.Multiple(
            () =>
            {
                Assert.That(module.ParseDelayMilliseconds("0:0:1"), Is.EqualTo(1000));
                Assert.That(module.ParseDelayMilliseconds("1500"), Is.EqualTo(1500));
                Assert.That(module.ParseDelayMilliseconds(2500), Is.EqualTo(2500));
                Assert.That(module.ParseDelayMilliseconds("invalid", 77), Is.EqualTo(77));
            }
        );
    }

    [Test]
    public void ParsePoint3D_WhenInputIsInvalid_ShouldReturnNull()
    {
        var module = new ConvertModule();

        var table = module.ParsePoint3D("not a point");

        Assert.That(table, Is.Null);
    }

    [Test]
    public void ParsePoint3D_WhenInputIsValid_ShouldReturnLuaTable()
    {
        var module = new ConvertModule();

        var table = module.ParsePoint3D("(1595, 2489, 20)");

        Assert.That(table, Is.Not.Null);
        Assert.Multiple(
            () =>
            {
                Assert.That(table!.Get("x").Type, Is.EqualTo(DataType.Number));
                Assert.That(table.Get("x").Number, Is.EqualTo(1595));
                Assert.That(table.Get("y").Number, Is.EqualTo(2489));
                Assert.That(table.Get("z").Number, Is.EqualTo(20));
            }
        );
    }

    [Test]
    public void ToBool_ShouldConvertCommonForms()
    {
        var module = new ConvertModule();

        Assert.Multiple(
            () =>
            {
                Assert.That(module.ToBool("true"), Is.True);
                Assert.That(module.ToBool("yes"), Is.True);
                Assert.That(module.ToBool(1), Is.True);
                Assert.That(module.ToBool("false"), Is.False);
                Assert.That(module.ToBool(0), Is.False);
            }
        );
    }

    [Test]
    public void ToInt_ShouldSupportDecimalAndHex()
    {
        var module = new ConvertModule();

        Assert.Multiple(
            () =>
            {
                Assert.That(module.ToInt("15"), Is.EqualTo(15));
                Assert.That(module.ToInt("0x1FE"), Is.EqualTo(510));
                Assert.That(module.ToInt(123.9), Is.EqualTo(123));
            }
        );
    }
}
