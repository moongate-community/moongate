using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types.Bank;

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

    /// <summary>
    ///     The gold of each player's bank; a withdrawal and a deposit move it and are recorded.
    /// </summary>
    public Dictionary<Serial, int> Gold { get; } = [];

    /// <summary>
    ///     What every withdrawal and deposit answers; null moves the gold and answers Ok.
    /// </summary>
    public BankResultType? Result { get; set; }

    public List<(MobileEntity Player, int Amount)> Withdrawn { get; } = [];

    public List<(MobileEntity Player, int Amount)> Deposited { get; } = [];

    public int? Balance(MobileEntity player)
    {
        return player.IsNpc ? null : Gold.GetValueOrDefault(player.Id);
    }

    public BankResultType Withdraw(MobileEntity player, int amount)
    {
        Withdrawn.Add((player, amount));

        if (Result is { } result)
        {
            return result;
        }

        Gold[player.Id] = Gold.GetValueOrDefault(player.Id) - amount;

        return BankResultType.Ok;
    }

    public BankResultType Deposit(MobileEntity player, int amount)
    {
        Deposited.Add((player, amount));

        if (Result is { } result)
        {
            return result;
        }

        Gold[player.Id] = Gold.GetValueOrDefault(player.Id) + amount;

        return BankResultType.Ok;
    }
}
