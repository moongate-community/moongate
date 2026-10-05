using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Commands;
using Moongate.Server.Ultima.Data.Targeting;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Types.Death;
using Moongate.Server.Ultima.Types.Targeting;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Ultima.Death;
using Moongate.Tests.TestSupport.Ultima.Targeting;

namespace Moongate.Tests.Server.Ultima.Commands;

public sealed class ResurrectCommandTests : IAsyncDisposable
{
    private static readonly Serial Corpse = new(0x40000900);

    private readonly StubTargetService _targets = new() { Result = TargetResult.ForObject(Corpse) };
    private readonly StubDeathService _death = new();

    private SessionFixture? _fixture;

    [Fact]
    public async Task ExecuteAsync_ACorpse_RaisesIt_AndSaysWhoIsBack()
    {
        _death.Raises = new(ResurrectResultType.Raised, new MobileEntity { Id = new(901), Name = "Tiara" });

        var context = await RunAsync();

        Assert.Equal([Corpse], _death.Raised);
        Assert.Equal("Tiara is back.", Assert.Single(context.Output).Text);
    }

    [Theory]
    [InlineData(ResurrectResultType.NotACorpse, "That is not a corpse.")]
    [InlineData(ResurrectResultType.CannotBeRaised, "That corpse cannot be raised.")]
    public async Task ExecuteAsync_WhatCannotBeRaised_SaysWhy(ResurrectResultType refusal, string text)
    {
        _death.Raises = new(refusal, null);

        var context = await RunAsync();

        Assert.Equal(text, Assert.Single(context.Output).Text);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheBirthFails_SaysTheCorpseCannotBeRaised()
    {
        _death.RaiseFailure = new InvalidOperationException("no serial left");

        var context = await RunAsync();

        Assert.Equal("That corpse cannot be raised.", Assert.Single(context.Output).Text);
    }

    [Fact]
    public async Task ExecuteAsync_TheCursorPutAway_RaisesNobody()
    {
        _targets.Result = TargetResult.Canceled(TargetCancelType.Canceled);

        var context = await RunAsync();

        Assert.Empty(_death.Raised);
        Assert.Equal("Target canceled.", Assert.Single(context.Output).Text);
    }

    public async ValueTask DisposeAsync()
    {
        if (_fixture is not null)
        {
            await _fixture.DisposeAsync();
        }
    }

    private async Task<CommandContext> RunAsync()
    {
        _fixture = await SessionFixture.CreateAsync();
        var session = new SessionService(_fixture.Loop).GetOrCreate(_fixture.Client);
        var context = new CommandContext(".resurrect", "resurrect", [], CommandSourceType.InGame, session);

        await new ResurrectCommand(_death, _targets).ExecuteAsync(context);

        return context;
    }
}
