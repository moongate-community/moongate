using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Tests.TestSupport.Ultima.Bank;

/// <summary>
///     Refuses the items a test locks, as a closed bank would, and records the players whose bank opened.
/// </summary>
public sealed class StubBankService : IBankService
{
    public HashSet<Serial> Locked { get; } = [];

    public List<MobileEntity> Opened { get; } = [];

    public bool Answer { get; set; } = true;

    public List<MobileEntity> Closed { get; } = [];

    public bool Open(MobileEntity player)
    {
        Opened.Add(player);

        return Answer;
    }

    public void OnSessionClosed(GameSession session)
    {
    }

    public void Close(MobileEntity player)
    {
        Closed.Add(player);
    }

    public bool IsOpen(MobileEntity player)
    {
        return Opened.Contains(player);
    }

    public bool CanAccess(GameSession session, MobileEntity character, ItemEntity item)
    {
        return !Locked.Contains(item.Id);
    }
}
