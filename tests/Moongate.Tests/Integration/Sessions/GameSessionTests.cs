using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Tests.Support.Sessions;

namespace Moongate.Tests.Integration.Sessions;

public sealed class GameSessionTests
{
    [Fact]
    public async Task SetAccountId_LoopWriteAndClearWorkWhileOffLoopWriteIsRejected()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var session = new GameSession(new(fixture.Client), fixture.Loop);

        Assert.Throws<InvalidOperationException>(() => session.SetAccountId(new(21)));
        await fixture.ExecuteOnLoopAsync(() => session.SetAccountId(new(21)));
        Assert.Equal(new(21), session.AccountId);

        Assert.Throws<InvalidOperationException>(() => session.SetAccountId(new(22)));
        Assert.Equal(new(21), session.AccountId);

        await fixture.ExecuteOnLoopAsync(() => session.SetAccountId(Serial.Zero));
        Assert.Equal(Serial.Zero, session.AccountId);
    }

    [Fact]
    public async Task SetAccountType_DefaultsToRegularAndOnlyLoopWritesApply()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var session = new GameSession(new(fixture.Client), fixture.Loop);

        Assert.Equal(AccountType.Regular, session.AccountType);

        Assert.Throws<InvalidOperationException>(() => session.SetAccountType(AccountType.Administrator));
        Assert.Equal(AccountType.Regular, session.AccountType);

        await fixture.ExecuteOnLoopAsync(() => session.SetAccountType(AccountType.GameMaster));
        Assert.Equal(AccountType.GameMaster, session.AccountType);
    }

    [Fact]
    public async Task SetCharacterId_OffLoopRejectedAndLoopWritePersists()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var network = new NetworkSession(fixture.Client);
        var session = new GameSession(network, fixture.Loop);

        Assert.Throws<InvalidOperationException>(() => session.SetCharacterId(new(42)));
        await fixture.ExecuteOnLoopAsync(() => session.SetCharacterId(new(42)));

        Assert.Equal(fixture.Client.SessionId, session.SessionId);
        Assert.Equal(new(42), session.CharacterId);
    }

    [Fact]
    public async Task SetCharacterId_RejectedOffLoopWritePreservesExistingValue()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var session = new GameSession(new(fixture.Client), fixture.Loop);
        await fixture.ExecuteOnLoopAsync(() => session.SetCharacterId(new(42)));

        Assert.Throws<InvalidOperationException>(() => session.SetCharacterId(new(84)));

        Assert.Equal(new(42), session.CharacterId);
    }

    [Fact]
    public async Task SetCharacterId_ZeroClearsAssociationOnLoop()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var session = new GameSession(new(fixture.Client), fixture.Loop);
        await fixture.ExecuteOnLoopAsync(() => session.SetCharacterId(new(42)));

        await fixture.ExecuteOnLoopAsync(() => session.SetCharacterId(Serial.Zero));

        Assert.Equal(Serial.Zero, session.CharacterId);
    }
}
