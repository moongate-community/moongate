using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Persistence;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class WorldPropsServiceTests
{
    private readonly RecordingDataAccess<WorldStateEntity> _data = new();

    [Fact]
    public async Task StartAsync_OnANewWorld_StartsWithNoProps()
    {
        var service = new WorldPropsService(_data);

        await service.StartAsync();

        Assert.Null(service.Get("event.day"));
        Assert.Equal(WorldStateEntity.RowId, service.State.Id);
    }

    [Fact]
    public async Task StartAsync_ReadsWhatTheLastSaveWrote()
    {
        _data.Upserted.Add(new() { Props = new() { ["event.day"] = 12L, ["motto"] = "hail" } });
        var service = new WorldPropsService(_data);

        await service.StartAsync();

        Assert.Equal((12L, "hail"), (service.Get("event.day"), service.Get("motto")));
    }

    [Fact]
    public void Set_KeepsReplacesAndRemoves()
    {
        var service = new WorldPropsService(_data);

        service.Set("event.day", 12L);
        service.Set("event.day", 13L);
        service.Set("open", true);
        service.Set("motto", "hail");
        service.Set("motto", null);
        service.Set("never", null);

        Assert.Equal(
            (13L, true, null, null),
            (service.Get("event.day"), service.Get("open"), service.Get("motto"), service.Get("never"))
        );
    }

    [Fact]
    public void Set_ABlankKeyOrAValueThatIsNoStringNumberOrBool_Throws()
    {
        var service = new WorldPropsService(_data);

        Assert.Throws<ArgumentException>(() => service.Set(" ", 1L));
        Assert.Throws<ArgumentException>(() => service.Set("list", new List<int>()));
    }

    // The save writes the copy while scripts keep changing the live one.
    [Fact]
    public void State_Snapshot_IsDetachedFromTheLiveProps()
    {
        var service = new WorldPropsService(_data);
        service.Set("event.day", 12L);

        var copy = service.State.Snapshot();
        service.Set("event.day", 13L);

        Assert.Equal(12L, copy.Props!["event.day"]);
        Assert.Equal(WorldStateEntity.RowId, copy.Id);
    }
}
