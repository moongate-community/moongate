using Moongate.Server.Types;

namespace Moongate.Tests.Server;

public class PlayerSessionTests
{
    [Fact]
    public void SessionStateType_Defaults_StartAtAwaitingSeed()
    {
        Assert.Equal((byte)0, (byte)SessionStateType.AwaitingSeed);
        Assert.Equal((byte)1, (byte)SessionStateType.Login);
        Assert.Equal((byte)2, (byte)SessionStateType.Authenticated);
    }
}
