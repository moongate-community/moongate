using Moongate.UO.Data.Hues;

namespace Moongate.Tests.UO.Data.Chat;

public class ChatHuesTests
{
    [Fact]
    public void Broadcast_Is0x35()
        => Assert.Equal((ushort)0x35, ChatHues.Broadcast.Value);

    [Fact]
    public void Default_Is0x3B2()
        => Assert.Equal((ushort)0x3B2, ChatHues.Default.Value);
}
