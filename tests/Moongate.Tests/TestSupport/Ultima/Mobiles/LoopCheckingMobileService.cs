using System.Diagnostics.CodeAnalysis;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Core.Types.Geometry;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Server.Ultima.Types.Movement;

namespace Moongate.Tests.TestSupport.Ultima.Mobiles;

/// <summary>
///     Passes every call to a real <see cref="IMobileService" /> and records whether each character entered the world on
///     the game loop.
/// </summary>
public sealed class LoopCheckingMobileService : IMobileService
{
    private readonly IMobileService _inner;
    private readonly IGameLoopService _gameLoop;

    public List<bool> EnteredOnLoop { get; } = [];

    public IReadOnlyCollection<Serial> InWorld => _inner.InWorld;

    public IReadOnlyCollection<MobileEntity> Mobiles => _inner.Mobiles;

    public LoopCheckingMobileService(IMobileService inner, IGameLoopService gameLoop)
    {
        _inner = inner;
        _gameLoop = gameLoop;
    }

    public Serial HairSerial(Serial mobile)
    {
        return _inner.HairSerial(mobile);
    }

    public Serial BeardSerial(Serial mobile)
    {
        return _inner.BeardSerial(mobile);
    }

    public void EnterWorld(MobileEntity mobile)
    {
        EnteredOnLoop.Add(_gameLoop.IsOnLoopThread);
        _inner.EnterWorld(mobile);
    }

    public bool TryGet(Serial serial, [NotNullWhen(true)] out MobileEntity? mobile)
    {
        return _inner.TryGet(serial, out mobile);
    }

    public bool LeaveWorld(Serial serial)
    {
        return _inner.LeaveWorld(serial);
    }

    public MoveResultType TryMove(
        MobileEntity mobile,
        DirectionType direction,
        MovementAbilityType ability = MovementAbilityType.Walk
    )
    {
        return _inner.TryMove(mobile, direction, ability);
    }

    public bool MoveTo(MobileEntity mobile, Point3D location)
    {
        return _inner.MoveTo(mobile, location);
    }

    public bool IsInWorld(Serial mobile)
    {
        return _inner.IsInWorld(mobile);
    }

    public bool Delete(Serial serial)
    {
        return _inner.Delete(serial);
    }

    public IReadOnlyCollection<Serial> Capture()
    {
        return _inner.Capture();
    }

    public void Committed(IReadOnlyCollection<Serial> serials)
    {
        _inner.Committed(serials);
    }

    public MobileFlagsType GetFlags(MobileEntity mobile)
    {
        return _inner.GetFlags(mobile);
    }

    public MobileStatusInfo GetStatus(MobileEntity mobile)
    {
        return _inner.GetStatus(mobile);
    }

    public List<MobileEquipmentEntry> GetEquipment(MobileEntity mobile, IEnumerable<ItemEntity> worn)
    {
        return _inner.GetEquipment(mobile, worn);
    }
}
