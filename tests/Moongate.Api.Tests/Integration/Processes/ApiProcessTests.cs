using Moongate.Api.Tests.TestSupport.Processes;
namespace Moongate.Api.Tests.Integration.Processes;
public class ApiProcessTests
{
    [Fact]
    public async Task Request_TwoProcesses_ReturnsTypedResponse()
    {
        await using var fixture = await ApiProcessFixture.StartAsync();
        Assert.Equal(42, await fixture.CallIncrementAsync(41));
    }

    [Fact]
    public async Task ThirdPeer_WithNoOperationPermission_ReceivesForbidden()
    {
        await using var fixture = await ApiProcessFixture.StartAsync();
        Assert.Equal("ERROR Forbidden", await fixture.CallForbiddenAsync());
    }

    [Fact]
    public async Task DirectGameRequest_StillWorksAfterLoginProcessStops()
    {
        await using var fixture = await ApiProcessFixture.StartAsync();
        await fixture.StopLoginAsync();
        Assert.Equal(42, await fixture.CallGameIncrementAsync(41));
    }

    [Fact]
    public async Task LostReply_ReconnectDoesNotReplayTheRequest()
    {
        await using var fixture = await ApiProcessFixture.StartAsync();
        Assert.Equal("DISCONNECTED RECONNECTED", await fixture.LoseReplyAndReconnectAsync());
        Assert.Equal(1, await fixture.GameInvocationCountAsync());
    }
}
