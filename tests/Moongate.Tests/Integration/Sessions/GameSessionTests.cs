using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Tests.Support.Sessions;

namespace Moongate.Tests.Integration.Sessions;

public sealed class GameSessionTests
{
    [Fact]
    public async Task Get_KeyWithoutValue_ReturnsTheKeyDefault()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var session = new GameSession(new(fixture.Client), fixture.Loop);
        var withDefault = new SessionKey<int>("Score", 7);
        var withoutDefault = new SessionKey<string?>("Title");

        Assert.Equal(7, session.Get(withDefault));
        Assert.Null(session.Get(withoutDefault));
        Assert.Equal(Serial.Zero, session.AccountId);
        Assert.Equal(Serial.Zero, session.CharacterId);
        Assert.Equal(AccountType.Regular, session.AccountType);
    }

    [Fact]
    public async Task Get_NullKey_ThrowsArgumentNullException()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var session = new GameSession(new(fixture.Client), fixture.Loop);

        Assert.Throws<ArgumentNullException>(() => session.Get<int>(null!));
    }

    [Fact]
    public async Task Set_AccountId_LoopWriteAndClearWorkWhileOffLoopWriteIsRejected()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var session = new GameSession(new(fixture.Client), fixture.Loop);

        Assert.Throws<InvalidOperationException>(() => session.Set(SessionKeys.AccountId, new(21)));
        await fixture.ExecuteOnLoopAsync(() => session.Set(SessionKeys.AccountId, new(21)));
        Assert.Equal(new(21), session.AccountId);

        Assert.Throws<InvalidOperationException>(() => session.Set(SessionKeys.AccountId, new(22)));
        Assert.Equal(new(21), session.AccountId);

        await fixture.ExecuteOnLoopAsync(() => session.Set(SessionKeys.AccountId, Serial.Zero));
        Assert.Equal(Serial.Zero, session.AccountId);
    }

    [Fact]
    public async Task Set_AccountType_DefaultsToRegularAndOnlyLoopWritesApply()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var session = new GameSession(new(fixture.Client), fixture.Loop);

        Assert.Equal(AccountType.Regular, session.AccountType);

        Assert.Throws<InvalidOperationException>(
            () => session.Set(SessionKeys.AccountType, AccountType.Administrator)
        );
        Assert.Equal(AccountType.Regular, session.AccountType);

        await fixture.ExecuteOnLoopAsync(() => session.Set(SessionKeys.AccountType, AccountType.GameMaster));
        Assert.Equal(AccountType.GameMaster, session.AccountType);
    }

    [Fact]
    public async Task Set_CharacterId_OffLoopRejectedAndLoopWritePersists()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var network = new NetworkSession(fixture.Client);
        var session = new GameSession(network, fixture.Loop);

        Assert.Throws<InvalidOperationException>(() => session.Set(SessionKeys.CharacterId, new(42)));
        await fixture.ExecuteOnLoopAsync(() => session.Set(SessionKeys.CharacterId, new(42)));

        Assert.Equal(fixture.Client.SessionId, session.SessionId);
        Assert.Equal(new(42), session.CharacterId);
    }

    [Fact]
    public async Task Set_CharacterId_RejectedOffLoopWritePreservesExistingValue()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var session = new GameSession(new(fixture.Client), fixture.Loop);
        await fixture.ExecuteOnLoopAsync(() => session.Set(SessionKeys.CharacterId, new(42)));

        Assert.Throws<InvalidOperationException>(() => session.Set(SessionKeys.CharacterId, new(84)));

        Assert.Equal(new(42), session.CharacterId);
    }

    [Fact]
    public async Task Set_CharacterId_ZeroClearsAssociationOnLoop()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var session = new GameSession(new(fixture.Client), fixture.Loop);
        await fixture.ExecuteOnLoopAsync(() => session.Set(SessionKeys.CharacterId, new(42)));

        await fixture.ExecuteOnLoopAsync(() => session.Set(SessionKeys.CharacterId, Serial.Zero));

        Assert.Equal(Serial.Zero, session.CharacterId);
    }

    [Fact]
    public async Task Set_KeyDeclaredOutsideTheCore_StoresAnyTypeAndKeepsValuesApart()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var session = new GameSession(new(fixture.Client), fixture.Loop);
        var guild = new SessionKey<string?>("Guild");
        var score = new SessionKey<int>("Score");
        var sameNameAsScore = new SessionKey<int>("Score");

        await fixture.ExecuteOnLoopAsync(
            () =>
            {
                session.Set(guild, "Order of the Moon");
                session.Set(score, 12);
            }
        );

        Assert.Equal("Order of the Moon", session.Get(guild));
        Assert.Equal(12, session.Get(score));
        Assert.Equal(0, session.Get(sameNameAsScore));
    }

    [Fact]
    public async Task Set_KeyWithoutValueIsNotSharedBetweenSessions()
    {
        await using var first = await SessionFixture.CreateAsync();
        await using var second = await SessionFixture.CreateAsync();
        var firstSession = new GameSession(new(first.Client), first.Loop);
        var secondSession = new GameSession(new(second.Client), second.Loop);

        await first.ExecuteOnLoopAsync(() => firstSession.Set(SessionKeys.CharacterId, new(42)));

        Assert.Equal(new(42), firstSession.CharacterId);
        Assert.Equal(Serial.Zero, secondSession.CharacterId);
    }

    [Fact]
    public async Task Set_NullKey_ThrowsArgumentNullException()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var session = new GameSession(new(fixture.Client), fixture.Loop);

        Assert.Throws<ArgumentNullException>(() => session.Set<int>(null!, 1));
    }
}
