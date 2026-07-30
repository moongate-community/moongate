using Moongate.Server.Abstractions.Data.Gumps;
using Moongate.Server.Abstractions.Types.Gumps;

namespace Moongate.Tests.Server.Gumps;

/// <summary>
/// A response is only as trustworthy as what the server remembers drawing. These are the checks
/// standing between the callback and a client that invents button numbers.
/// </summary>
public class OpenGumpTests
{
    [Fact]
    public void Validate_ADrawnButton_IsAccepted()
        => Assert.Equal(GumpRejectionType.None, Gump().Validate(7, [], new Dictionary<int, string>()));

    [Fact]
    public void Validate_AButtonThatWasNeverDrawn_IsRejected()
        => Assert.Equal(GumpRejectionType.UnknownButton, Gump().Validate(99, [], new Dictionary<int, string>()));

    // Zero is the client's own close button and is never drawn by the server, so it has to pass.
    [Fact]
    public void Validate_ButtonZero_IsAlwaysAccepted()
        => Assert.Equal(GumpRejectionType.None, Gump().Validate(0, [], new Dictionary<int, string>()));

    [Fact]
    public void Validate_ASwitchThatWasNeverDrawn_IsRejected()
        => Assert.Equal(GumpRejectionType.UnknownSwitch, Gump().Validate(7, [42], new Dictionary<int, string>()));

    [Fact]
    public void Validate_ATextEntryThatWasNeverDrawn_IsRejected()
        => Assert.Equal(
            GumpRejectionType.UnknownTextEntry,
            Gump().Validate(7, [], new Dictionary<int, string> { [42] = "hi" })
        );

    // 239 is the client's own limit; anything longer did not come from the client's text field.
    [Fact]
    public void Validate_TextLongerThanTheClientAllows_IsRejected()
        => Assert.Equal(
            GumpRejectionType.TextTooLong,
            Gump().Validate(7, [], new Dictionary<int, string> { [5] = new('x', 240) })
        );

    [Fact]
    public void Validate_TextAtTheLimit_IsAccepted()
        => Assert.Equal(
            GumpRejectionType.None,
            Gump().Validate(7, [], new Dictionary<int, string> { [5] = new('x', 239) })
        );

    [Fact]
    public void Validate_EverythingDrawn_IsAccepted()
        => Assert.Equal(
            GumpRejectionType.None,
            Gump().Validate(7, [3], new Dictionary<int, string> { [5] = "hi" })
        );

    private static OpenGump Gump()
        => new(1u, 2, "test", new HashSet<int> { 7 }, new HashSet<int> { 3 }, new HashSet<int> { 5 }, null);
}
