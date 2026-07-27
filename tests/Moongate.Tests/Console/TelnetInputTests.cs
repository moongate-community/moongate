using System.Text;
using Moongate.Console.Admin.Plugin.Services.Console;

namespace Moongate.Tests.Console;

public class TelnetInputTests
{
    [Fact]
    public void StripControls_PlainText_IsTrimmedUnchanged()
        => Assert.Equal("broadcast hi", TelnetInput.StripControls("  broadcast hi  "));

    [Fact]
    public void StripControls_RemovesControlAndReplacementChars()
    {
        // A telnet IAC negotiation (0xFF 0xFB 0x01) decodes to replacement chars under UTF-8.
        var decoded = Encoding.UTF8.GetString([0xFF, 0xFB, 0x01]) + "help";

        Assert.Equal("help", TelnetInput.StripControls(decoded));
    }
}
