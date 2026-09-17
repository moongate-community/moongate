using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Tests.Support.Sessions;

namespace Moongate.Tests.Integration.Sessions;

public sealed class GameSessionTests
{
    [Fact]
    public async Task SetCharacterId_OffLoopRejectedAndLoopWritePersists()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var network = new NetworkSession(fixture.Client);
        var session = new GameSession(network, fixture.Loop);

        Assert.Throws<InvalidOperationException>(() => session.SetCharacterId(new Serial(42)));
        await fixture.ExecuteOnLoopAsync(() => session.SetCharacterId(new Serial(42)));

        Assert.Equal(fixture.Client.SessionId, session.SessionId);
        Assert.Equal(new Serial(42), session.CharacterId);
    }

    [Fact]
    public async Task SetCharacterId_RejectedOffLoopWritePreservesExistingValue()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var session = new GameSession(new NetworkSession(fixture.Client), fixture.Loop);
        await fixture.ExecuteOnLoopAsync(() => session.SetCharacterId(new Serial(42)));

        Assert.Throws<InvalidOperationException>(() => session.SetCharacterId(new Serial(84)));

        Assert.Equal(new Serial(42), session.CharacterId);
    }

    [Fact]
    public async Task SetAccountId_LoopWriteAndClearWorkWhileOffLoopWriteIsRejected()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var session = new GameSession(new NetworkSession(fixture.Client), fixture.Loop);

        Assert.Throws<InvalidOperationException>(() => session.SetAccountId(new Serial(21)));
        await fixture.ExecuteOnLoopAsync(() => session.SetAccountId(new Serial(21)));
        Assert.Equal(new Serial(21), session.AccountId);

        Assert.Throws<InvalidOperationException>(() => session.SetAccountId(new Serial(22)));
        Assert.Equal(new Serial(21), session.AccountId);

        await fixture.ExecuteOnLoopAsync(() => session.SetAccountId(Serial.Zero));
        Assert.Equal(Serial.Zero, session.AccountId);
    }

    [Fact]
    public async Task SetCharacterId_ZeroClearsAssociationOnLoop()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var session = new GameSession(new NetworkSession(fixture.Client), fixture.Loop);
        await fixture.ExecuteOnLoopAsync(() => session.SetCharacterId(new Serial(42)));

        await fixture.ExecuteOnLoopAsync(() => session.SetCharacterId(Serial.Zero));

        Assert.Equal(Serial.Zero, session.CharacterId);
    }
}
