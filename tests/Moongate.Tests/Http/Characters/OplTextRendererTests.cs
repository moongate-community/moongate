using Moongate.Http.Plugin.Services.Characters;
using Moongate.Tests.Support;

namespace Moongate.Tests.Http.Characters;

/// <summary>
/// A property line is a cliloc plus TAB-separated arguments, and an argument may itself be a cliloc.
/// Rendering that is UO's grammar, which is exactly why it happens here and not in the browser.
/// </summary>
public class OplTextRendererTests
{
    [Fact]
    public void Render_AClilocWithNoArguments_IsItsText()
    {
        var renderer = new OplTextRenderer(Clilocs((500000, "a dagger")));

        Assert.Equal("a dagger", renderer.Render(new(500000, "")));
    }

    // How OplService emits free text: a string cliloc whose whole body is one slot.
    [Fact]
    public void Render_AStringClilocIsItsArgument()
    {
        var renderer = new OplTextRenderer(Clilocs((1042971, "~1_val~")));

        Assert.Equal("Tommy's spellbook", renderer.Render(new(1042971, "Tommy's spellbook")));
    }

    // The string table is client data and may be absent or incomplete. A line nothing describes is
    // dropped by the caller, so null says "no line" rather than showing "~1_WEIGHT~" to a reader.
    [Fact]
    public void Render_IsNullWhenTheTableDoesNotDescribeTheCliloc()
        => Assert.Null(new OplTextRenderer(Clilocs()).Render(new(1072788, "3")));

    // An unresolvable nested cliloc must not leave "#1020001" in the sentence.
    [Fact]
    public void Render_LeavesNoPlaceholderWhenAnArgumentCannotBeResolved()
    {
        var renderer = new OplTextRenderer(Clilocs((1050039, "~1_NUMBER~ ~2_ITEMNAME~")));

        var line = renderer.Render(new(1050039, "500\t#1020001"));

        Assert.DoesNotContain("#", line);
        Assert.DoesNotContain("~", line);
    }

    // An argument starting with # is itself a cliloc: the stack line names its item that way, and the
    // prefix is load-bearing rather than decoration.
    [Fact]
    public void Render_ResolvesAnArgumentThatIsItselfACliloc()
    {
        var renderer = new OplTextRenderer(Clilocs((1050039, "~1_NUMBER~ ~2_ITEMNAME~"), (1020001, "gold coins")));

        Assert.Equal("500 gold coins", renderer.Render(new(1050039, "500\t#1020001")));
    }

    [Fact]
    public void Render_SubstitutesASingleArgument()
    {
        var renderer = new OplTextRenderer(Clilocs((1072788, "Weight: ~1_WEIGHT~ stone")));

        Assert.Equal("Weight: 3 stone", renderer.Render(new(1072788, "3")));
    }

    // Multi-slot arguments arrive TAB-separated, in slot order.
    [Fact]
    public void Render_SubstitutesEachSlotInTurn()
    {
        var renderer = new OplTextRenderer(Clilocs((1050039, "~1_NUMBER~ ~2_ITEMNAME~")));

        Assert.Equal("500 gold coins", renderer.Render(new(1050039, "500\tgold coins")));
    }

    // Fewer arguments than slots is malformed data, not a crash.
    [Fact]
    public void Render_ToleratesMissingArguments()
    {
        var renderer = new OplTextRenderer(Clilocs((1050039, "~1_NUMBER~ ~2_ITEMNAME~")));

        var line = renderer.Render(new(1050039, "500"));

        Assert.NotNull(line);
        Assert.DoesNotContain("~", line);
    }

    private static StubClilocService Clilocs(params (int Cliloc, string Text)[] entries)
        => StubClilocService.Entries(entries);
}
