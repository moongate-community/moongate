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

    /// <summary>
    ///     The coins each player carries; a withdrawal that moves the gold adds to them, taking carried gold removes from them.
    /// </summary>
    public Dictionary<Serial, long> Carried { get; } = [];

    public List<(MobileEntity Player, int Amount)> Taken { get; } = [];

    public long CarriedGold(MobileEntity player)
    {
        return Carried.GetValueOrDefault(player.Id);
    }

    public bool TakeCarriedGold(MobileEntity player, int amount)
    {
        if (amount < 1 || Carried.GetValueOrDefault(player.Id) < amount)
        {
            return false;
        }

        Carried[player.Id] -= amount;
        Taken.Add((player, amount));

        return true;
    }

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
        Carried[player.Id] = Carried.GetValueOrDefault(player.Id) + amount;

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

    public List<(MobileEntity Player, int Amount)> Checks { get; } = [];

    public List<(MobileEntity Player, ItemEntity Check)> Cashed { get; } = [];

    /// <summary>
    ///     What cashing a check deposits; the worth of each item, for those that are checks.
    /// </summary>
    public int CashDeposits { get; set; }

    public Dictionary<Serial, long> Worths { get; } = [];

    public BankResultType WriteCheck(MobileEntity player, int amount)
    {
        Checks.Add((player, amount));

        return Result ?? BankResultType.Ok;
    }

    /// <summary>
    ///     What a cashing that is not refused does to the check, such as changing what it is worth.
    /// </summary>
    public Action<ItemEntity>? OnCash { get; set; }

    public BankResultType Cash(MobileEntity player, ItemEntity check, out int deposited)
    {
        Cashed.Add((player, check));
        deposited = Result is null ? CashDeposits : 0;

        if (Result is null)
        {
            OnCash?.Invoke(check);
        }

        return Result ?? BankResultType.Ok;
    }

    public List<(MobileEntity Player, ItemEntity Item)> DepositedItems { get; } = [];

    /// <summary>
    ///     What an item handed to the bank adds to the player's gold when it is not refused.
    /// </summary>
    public int ItemDeposits { get; set; }

    public BankResultType DepositItem(MobileEntity player, ItemEntity item)
    {
        DepositedItems.Add((player, item));

        if (Result is { } result)
        {
            return result;
        }

        Gold[player.Id] = Gold.GetValueOrDefault(player.Id) + ItemDeposits;

        return BankResultType.Ok;
    }

    public long? WorthOf(ItemEntity item)
    {
        return Worths.TryGetValue(item.Id, out var worth) ? worth : null;
    }
}
