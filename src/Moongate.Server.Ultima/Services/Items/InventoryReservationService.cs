using Moongate.Core.Primitives;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Interfaces.Items;
namespace Moongate.Server.Ultima.Services.Items;
/// <inheritdoc />
public sealed class InventoryReservationService : IInventoryReservationService
{
    private readonly IGameLoopService _loop;
    private readonly Dictionary<Serial, Task> _settlements = [];
    private Serial? _applying;
    public InventoryReservationService(IGameLoopService loop)
    {
        _loop = loop;
    }
    public bool TryReserve(Serial mobileId, Task settlement)
    {
        EnsureLoop();
        return _settlements.TryAdd(mobileId, settlement);
    }
    public bool IsReserved(Serial mobileId)
    {
        EnsureLoop();
        return _settlements.ContainsKey(mobileId) && _applying != mobileId;
    }
    public Task WaitAsync(Serial mobileId)
    {
        EnsureLoop();
        return _settlements.GetValueOrDefault(mobileId) ?? Task.CompletedTask;
    }
    public void Release(Serial mobileId)
    {
        EnsureLoop();
        _settlements.Remove(mobileId);
    }
    public void Apply(Serial mobileId, Action application)
    {
        EnsureLoop();
        if (!_settlements.ContainsKey(mobileId) || _applying is not null)
        {
            throw new InvalidOperationException("Inventory application requires an exclusive reservation.");
        }
        _applying = mobileId;
        try
        {
            application();
        }
        finally
        {
            _applying = null;
        }
    }
    private void EnsureLoop()
    {
        if (!_loop.IsOnLoopThread)
        {
            throw new InvalidOperationException("Inventory reservations belong to the game loop.");
        }
    }
}
