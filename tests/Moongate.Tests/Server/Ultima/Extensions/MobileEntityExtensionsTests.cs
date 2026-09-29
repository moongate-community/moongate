using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Extensions;

namespace Moongate.Tests.Server.Ultima.Extensions;

public sealed class MobileEntityExtensionsTests
{
    [Fact]
    public void DisplayName_WithATitle_AppendsIt()
    {
        Assert.Equal("Bob the Weaponsmith", new MobileEntity { Name = "Bob", Title = "the Weaponsmith" }.DisplayName());
    }

    [Theory, InlineData(null), InlineData(""), InlineData("  ")]
    public void DisplayName_WithoutATitle_IsTheName(string? title)
    {
        Assert.Equal("Aria", new MobileEntity { Name = "Aria", Title = title }.DisplayName());
    }

    [Fact]
    public void DisplayName_WithoutAName_FallsBackToTheTemplate()
    {
        Assert.Equal("orc", new MobileEntity { Name = "", TemplateId = "orc" }.DisplayName());
    }

    [Fact]
    public void DisplayName_WithoutNameOrTemplate_FallsBackToTheSerial()
    {
        Assert.Equal("0x00000007", new MobileEntity { Id = new(7), Name = "" }.DisplayName());
    }
}
