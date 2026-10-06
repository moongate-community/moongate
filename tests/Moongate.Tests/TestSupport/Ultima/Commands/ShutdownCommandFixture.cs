using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Services.Hosting;
using Moongate.Server.Services.Timing;
using Moongate.Server.Ultima.Commands;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.Support.Timing;
using Moongate.Tests.TestSupport.Ultima.Speech;

namespace Moongate.Tests.TestSupport.Ultima.Commands;

public sealed class ShutdownCommandFixture : IAsyncDisposable
{
    public BroadcastFixture World { get; }
    public ManualTimeProvider Clock { get; } = new();
    public TimerWheelService Timers { get; }
    public ServerShutdownService Shutdown { get; } = new();
    public ShutdownCommand Command { get; }

    public static async Task<ShutdownCommandFixture> CreateAsync()
    {
        var fixture = new ShutdownCommandFixture(await BroadcastFixture.CreateAsync());
        await fixture.World.AddAsync(1);
        await fixture.World.Network.ExecuteOnLoopAsync(() => fixture.Timers.BindToCurrentThread(() => { }));

        return fixture;
    }

    public Task AdvanceAsync(TimeSpan elapsed)
    {
        return World.Network.ExecuteOnLoopAsync(() =>
            {
                Clock.Advance(elapsed);
                Timers.ProcessDueTimers();
            }
        );
    }

    public CommandContext Context(string arguments = "", CancellationToken cancellationToken = default)
    {
        return new(
            "shutdown " + arguments,
            "shutdown",
            arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries),
            CommandSourceType.Console,
            null,
            cancellationToken
        );
    }

    private ShutdownCommandFixture(BroadcastFixture world)
    {
        World = world;
        Timers = new(new() { TickDuration = TimeSpan.FromMilliseconds(1), MaxPendingTimers = 1 }, Clock);
        Command = new(
            Shutdown,
            Timers,
            new BroadcastService(world.Network.Loop, world.Sessions, world.Mobiles, world.Sender)
        );
    }

    public async ValueTask DisposeAsync()
    {
        await Timers.StopAsync();
        await World.DisposeAsync();
    }
}
