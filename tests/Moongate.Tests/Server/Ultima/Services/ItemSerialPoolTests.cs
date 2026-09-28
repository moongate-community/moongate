using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Persistence;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class ItemSerialPoolTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly RecordingDataAccess<ItemEntity> _data = new();

    [Fact]
    public async Task StartAsync_FillsThePool()
    {
        var pool = new ItemSerialPool(_data);

        await pool.StartAsync();

        Assert.Equal(ItemSerialPool.Capacity, _data.Reserved);
        Assert.True(pool.TryTake(out var serial));
        Assert.Equal(new Serial(0x40000100), serial);
        await pool.StopAsync();
    }

    [Fact]
    public void TryTake_BeforeTheFirstFill_ReturnsFalse()
    {
        Assert.False(new ItemSerialPool(_data).TryTake(out _));
    }

    [Fact]
    public async Task TryTake_BelowTheThreshold_RefillsThePool()
    {
        var pool = new ItemSerialPool(_data);
        await pool.StartAsync();

        for (var taken = 0; taken <= ItemSerialPool.Capacity - ItemSerialPool.RefillBelow; taken++)
        {
            Assert.True(pool.TryTake(out _));
        }

        Assert.True(SpinWait.SpinUntil(() => _data.Reserved > ItemSerialPool.Capacity, Timeout));
        await pool.StopAsync();
    }

    [Fact]
    public async Task TryTake_AfterAFailedRefill_TriesAgain()
    {
        _data.FailReservations = new InvalidOperationException("database down");
        var pool = new ItemSerialPool(_data);
        await pool.StartAsync();
        Assert.False(pool.TryTake(out _));
        await pool.StopAsync();
        _data.FailReservations = null;

        Assert.False(pool.TryTake(out _));

        Assert.True(SpinWait.SpinUntil(() => pool.TryTake(out _), Timeout));
        await pool.StopAsync();
    }
}
