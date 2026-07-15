using Moongate.Server.Handlers;

namespace Moongate.Tests.Server.Handlers;

public class GeneralInformationHandlerTests
{
    [Fact]
    public void ParseScreenSize_ReadsWidthAndHeight()
    {
        // 2 bytes unknown, width 800 (0x0320), height 600 (0x0258).
        var payload = new byte[] { 0x00, 0x00, 0x03, 0x20, 0x02, 0x58 };

        var size = GeneralInformationHandler.ParseScreenSize(payload);

        Assert.Equal((800, 600), size);
    }

    [Fact]
    public void ParseScreenSize_ShortPayload_ReturnsNull()
        => Assert.Null(GeneralInformationHandler.ParseScreenSize([0x00, 0x00]));

    [Fact]
    public void ParseLanguage_ReadsThreeCharCode()
        => Assert.Equal("ENU", GeneralInformationHandler.ParseLanguage([(byte)'E', (byte)'N', (byte)'U', 0x00]));

    [Fact]
    public void ParseLanguage_EmptyPayload_ReturnsNull()
        => Assert.Null(GeneralInformationHandler.ParseLanguage([]));
}
