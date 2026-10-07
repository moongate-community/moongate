using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Services.Items;
using Moongate.Tests.TestSupport.Scripting;

namespace Moongate.Tests.Server.Ultima.Services.Items;

public sealed class InventoryReservationServiceTests
{
    [Fact]
    public void Reserve_ExcludesUntilReleaseAndRestoresPermitAfterFailure()
    {
        var reservations = new InventoryReservationService(new StubGameLoop());
        var finished = new TaskCompletionSource();
        var owner = new Serial(2);
        Assert.True(reservations.TryReserve(owner, finished.Task));
        Assert.False(reservations.TryReserve(owner, Task.CompletedTask));
        Assert.True(reservations.IsReserved(owner));
        Assert.Same(finished.Task, reservations.WaitAsync(owner));
        Assert.Throws<InvalidOperationException>(() => reservations.Apply(
                owner,
                () =>
                {
                    Assert.False(reservations.IsReserved(owner));
                    throw new InvalidOperationException();
                }
            )
        );
        Assert.True(reservations.IsReserved(owner));
        reservations.Release(owner);
        Assert.False(reservations.IsReserved(owner));
    }

    [Fact]
    public void Reserve_OffLoopRefuses()
    {
        var reservations = new InventoryReservationService(new StubGameLoop { IsOnLoopThread = false });
        Assert.Throws<InvalidOperationException>(() => reservations.TryReserve(new(2), Task.CompletedTask));
    }
}
