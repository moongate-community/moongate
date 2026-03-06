using Moongate.Server.Interfaces.Items;
using Moongate.Server.Modules;
using Moongate.UO.Data.Ids;

namespace Moongate.Tests.Server.Modules;

public sealed class DoorModuleTests
{
    private sealed class DoorModuleTestDoorService : IDoorService
    {
        public bool IsDoorResult { get; set; }
        public bool ToggleResult { get; set; }
        public Serial LastIsDoorSerial { get; private set; }
        public Serial LastToggleSerial { get; private set; }

        public Task<bool> IsDoorAsync(Serial itemId, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            LastIsDoorSerial = itemId;

            return Task.FromResult(IsDoorResult);
        }

        public Task<bool> ToggleAsync(Serial itemId, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            LastToggleSerial = itemId;

            return Task.FromResult(ToggleResult);
        }
    }

    [Test]
    public void IsDoor_WhenSerialIsZero_ShouldReturnFalse()
    {
        var service = new DoorModuleTestDoorService();
        var module = new DoorModule(service);

        var result = module.IsDoor(0);

        Assert.That(result, Is.False);
    }

    [Test]
    public void IsDoor_WhenServiceReturnsTrue_ShouldReturnTrue()
    {
        var service = new DoorModuleTestDoorService
        {
            IsDoorResult = true
        };
        var module = new DoorModule(service);

        var result = module.IsDoor(0x40000001);

        Assert.Multiple(
            () =>
            {
                Assert.That(result, Is.True);
                Assert.That(service.LastIsDoorSerial, Is.EqualTo((Serial)0x40000001u));
            }
        );
    }

    [Test]
    public void Toggle_WhenSerialIsZero_ShouldReturnFalse()
    {
        var service = new DoorModuleTestDoorService();
        var module = new DoorModule(service);

        var result = module.Toggle(0);

        Assert.That(result, Is.False);
    }

    [Test]
    public void Toggle_WhenServiceReturnsTrue_ShouldReturnTrue()
    {
        var service = new DoorModuleTestDoorService
        {
            ToggleResult = true
        };
        var module = new DoorModule(service);

        var result = module.Toggle(0x40000001);

        Assert.Multiple(
            () =>
            {
                Assert.That(result, Is.True);
                Assert.That(service.LastToggleSerial, Is.EqualTo((Serial)0x40000001u));
            }
        );
    }
}
