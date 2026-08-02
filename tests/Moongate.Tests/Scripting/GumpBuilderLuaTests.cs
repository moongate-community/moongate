using Moongate.Server.Scripting.Refs;
using Moongate.Server.Services.Gumps;
using MoonSharp.Interpreter;

namespace Moongate.Tests.Scripting;

/// <summary>
/// The named-table form only works if MoonSharp binds a Lua table to a C# <c>Table</c> parameter and
/// leaves a missing key readable as absent. Neither is worth assuming — the marshaller has
/// contradicted expectations twice in this codebase — so both are exercised through real Lua here.
/// </summary>
public class GumpBuilderLuaTests
{
    [Fact]
    public void ASuppliedOptionalKey_IsUsed()
    {
        var (script, builder) = Bind();

        script.DoString("g.image { x = 10, y = 20, art = 100, hue = 33 }");

        Assert.Equal("{ gumppic 10 20 100 hue=33 }", builder.Layout);
    }

    // The whole point of named fields: an omitted key is simply absent, so the element takes its
    // default rather than shifting every following argument along one.
    [Fact]
    public void AnOmittedKey_FallsBackToTheDefault()
    {
        var (script, builder) = Bind();

        script.DoString("g.image { x = 10, y = 20, art = 100 }");

        Assert.Equal("{ gumppic 10 20 100 }", builder.Layout);
    }

    [Fact]
    public void IdsDrawnFromLua_AreRecordedForValidation()
    {
        var (script, builder) = Bind();

        script.DoString(
            """
            g.button { x = 0, y = 0, art = 4005, pressed = 4007, id = 7 }
            g.check { x = 0, y = 20, art = 210, pressed = 211, id = 3, checked = true }
            g.entry { x = 0, y = 40, w = 100, h = 20, id = 5 }
            """
        );

        Assert.Equal([7], builder.ButtonIds);
        Assert.Equal([3], builder.SwitchIds);
        Assert.Equal([5], builder.TextEntryIds);
    }

    [Fact]
    public void NamedTables_ReachTheBuilder()
    {
        var (script, builder) = Bind();

        script.DoString(
            """
            g.background { x = 0, y = 0, w = 300, h = 200, art = 5054 }
            g.label { x = 20, y = 20, hue = 1153, text = "Banca di Britain" }
            """
        );

        Assert.Equal("{ resizepic 0 0 5054 300 200 }{ text 20 20 1153 0 }", builder.Layout);
        Assert.Equal(["Banca di Britain"], builder.Strings);
    }

    // The three localized forms are chosen by which optional keys are present, which is what
    // replaces C#'s overloads.
    [Fact]
    public void TheLocalizedFormIsChosenByWhichKeysArePresent()
    {
        var (script, builder) = Bind();

        script.DoString(
            """
            g.html_localized { x = 0, y = 0, w = 100, h = 50, cliloc = 1049644 }
            g.html_localized { x = 0, y = 0, w = 100, h = 50, cliloc = 1049644, color = 32767 }
            g.html_localized { x = 0, y = 0, w = 100, h = 50, cliloc = 1049644, color = 32767, args = "Squid" }
            """
        );

        Assert.Contains("{ xmfhtmlgump 0 0 100 50 1049644 0 0 }", builder.Layout);
        Assert.Contains("{ xmfhtmlgumpcolor 0 0 100 50 1049644 0 0 32767 }", builder.Layout);
        Assert.Contains("{ xmfhtmltok 0 0 100 50 0 0 32767 1049644 @Squid@ }", builder.Layout);
    }

    private static (Script Script, GumpBuilder Builder) Bind()
    {
        var script = new Script();
        var builder = new GumpBuilder();

        script.Globals["g"] = new GumpBuilderFactory(script).Create(builder);

        return (script, builder);
    }
}
