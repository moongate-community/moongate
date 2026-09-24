using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Tests.TestSupport.Network;

namespace Moongate.Tests.Server.Core.Data.Sessions;

public sealed class LoginSessionTests
{
    [Fact]
    public void TryGetAuthenticatedAccount_ReturnsAnIndependentProofSnapshot()
    {
        using var connection = new ControlledNetworkConnection(1);
        var session = new LoginSession(connection);
        var key = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();

        Assert.True(session.TrySetAccount(new Serial(42), AccountType.Regular, "Alice", key));
        Array.Clear(key);
        Assert.True(session.TryGetAuthenticatedAccount(out var accountId, out var accountType,
            out var username, out var snapshot));
        Assert.Equal(new Serial(42), accountId);
        Assert.Equal(AccountType.Regular, accountType);
        Assert.Equal("Alice", username);
        Assert.Equal(Enumerable.Range(0, 32).Select(value => (byte)value), snapshot);

        Array.Clear(snapshot!);
        Assert.True(session.TryGetAuthenticatedAccount(out _, out _, out _, out var second));
        Assert.Equal(Enumerable.Range(0, 32).Select(value => (byte)value), second);
    }

    [Fact]
    public void ClearAccount_DropsTheAuthenticatedProof()
    {
        using var connection = new ControlledNetworkConnection(1);
        var session = new LoginSession(connection);
        Assert.True(session.TrySetAccount(new Serial(42), AccountType.Regular, "alice", new byte[32]));

        session.ClearAccount();

        Assert.False(session.TryGetAuthenticatedAccount(out _, out _, out _, out _));
        Assert.Equal(Serial.Zero, session.AccountId);
    }

    [Fact]
    public void Disconnect_DropsTheAuthenticatedProofAndPreventsReplacement()
    {
        using var connection = new ControlledNetworkConnection(1);
        var session = new LoginSession(connection);
        Assert.True(session.TrySetAccount(new Serial(42), AccountType.Regular, "alice", new byte[32]));

        session.Disconnect();

        Assert.False(session.TryGetAuthenticatedAccount(out _, out _, out _, out _));
        Assert.False(session.TrySetAccount(new Serial(43), AccountType.Regular, "bob", new byte[32]));
    }

    [Fact]
    public void TrySetAccount_WithoutProofClearsAnEarlierProof()
    {
        using var connection = new ControlledNetworkConnection(1);
        var session = new LoginSession(connection);
        Assert.True(session.TrySetAccount(new Serial(42), AccountType.Regular, "alice", new byte[32]));

        Assert.True(session.TrySetAccount(new Serial(43), AccountType.Regular));

        Assert.False(session.TryGetAuthenticatedAccount(out _, out _, out _, out _));
        Assert.Equal(new Serial(43), session.AccountId);
    }

    [Fact]
    public void TrySetAccount_ZeroIdentityDoesNotRetainCredentialKey()
    {
        using var connection = new ControlledNetworkConnection(1);
        var session = new LoginSession(connection);

        Assert.False(session.TrySetAccount(Serial.Zero, AccountType.Regular, "alice", new byte[32]));
        Assert.False(session.TryGetAuthenticatedAccount(out _, out _, out _, out _));
    }
}
