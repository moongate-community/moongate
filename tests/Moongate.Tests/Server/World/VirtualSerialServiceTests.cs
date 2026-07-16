using Moongate.Core.Primitives;
using Moongate.Server.Services.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.World;

public class VirtualSerialServiceTests
{
    [Fact]
    public void GetOrCreate_AllocatesFromTheReservedBand()
    {
        var serial = new VirtualSerialService().GetOrCreate(new Serial(0x64), LayerType.Hair);

        Assert.True(serial.IsVirtual);
        Assert.False(serial.IsItem); // the whole point: it can never be mistaken for a real item
    }

    [Fact]
    public void GetOrCreate_SameOwnerAndLayer_KeepsGivingTheSameSerial()
    {
        var service = new VirtualSerialService();
        var owner = new Serial(0x64);

        Assert.Equal(
            service.GetOrCreate(owner, LayerType.Hair),
            service.GetOrCreate(owner, LayerType.Hair)
        );
    }

    [Fact]
    public void GetOrCreate_DifferentLayersOfOneOwner_GetDifferentSerials()
    {
        var service = new VirtualSerialService();
        var owner = new Serial(0x64);

        Assert.NotEqual(
            service.GetOrCreate(owner, LayerType.Hair),
            service.GetOrCreate(owner, LayerType.FacialHair)
        );
    }

    [Fact]
    public void GetOrCreate_DifferentOwners_GetDifferentSerials()
    {
        // The bug this exists to prevent: every mobile's hair sharing one serial, so a client seeing two
        // people would treat their hair as the same object.
        var service = new VirtualSerialService();

        Assert.NotEqual(
            service.GetOrCreate(new Serial(0x64), LayerType.Hair),
            service.GetOrCreate(new Serial(0x65), LayerType.Hair)
        );
    }

    [Fact]
    public void GetOrCreate_AcrossManyOwners_StaysInTheBandAndNeverRepeats()
    {
        var service = new VirtualSerialService();
        var seen = new HashSet<Serial>();

        for (var owner = 1u; owner <= 1000; owner++)
        {
            var serial = service.GetOrCreate(new Serial(owner), LayerType.Hair);

            Assert.True(serial.IsVirtual);
            Assert.True(seen.Add(serial), $"serial {serial} was handed out twice");
        }
    }
}
