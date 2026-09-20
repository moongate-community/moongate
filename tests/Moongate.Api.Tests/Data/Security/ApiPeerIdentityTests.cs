using Moongate.Api.Data.Security;

namespace Moongate.Api.Tests.Data.Security;

public sealed class ApiPeerIdentityTests
{
    [Theory, InlineData(1), InlineData(100), InlineData(65535)]
    public void CanInvoke_AllOperations_GrantsEveryNonzeroIdentifier(ushort operation)
    {
        var peer = new ApiPeerIdentity("realm", [], allowAllOperations: true);
        Assert.True(peer.CanInvoke(operation));
        Assert.False(peer.CanInvoke(0));
    }

    [Fact]
    public void CanInvoke_EmptyExplicitList_DeniesEveryOperation()
    {
        var peer = new ApiPeerIdentity("realm", []);
        Assert.False(peer.CanInvoke(0));
        Assert.False(peer.CanInvoke(1));
        Assert.False(peer.CanInvoke(65535));
    }

    [Fact]
    public void Constructor_AllOperationsWithReservedIdentifier_StillRejectsInvalidPolicy()
    {
        Assert.Throws<ArgumentException>(() => new ApiPeerIdentity("realm", [0], allowAllOperations: true));
    }
}
