using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types.Guilds;

namespace Moongate.Tests.TestSupport.Ultima.Vendors;

/// <summary>
///     Makes every NPC a guildmaster of the guild a test sets, and records the words and the gold it hears.
/// </summary>
public sealed class RecordingNpcGuildService : INpcGuildService
{
    public NpcGuildType? Guild { get; set; }

    public bool Answer { get; set; } = true;

    public List<MobileEntity> Quoted { get; } = [];

    public List<MobileEntity> Resigned { get; } = [];

    public List<ItemEntity> Joined { get; } = [];

    public NpcGuildType? Of(MobileEntity guildmaster)
    {
        return Guild;
    }

    public NpcGuildType? MemberOf(MobileEntity player)
    {
        return null;
    }

    public bool Quote(MobileEntity guildmaster, MobileEntity player)
    {
        Quoted.Add(player);

        return Answer;
    }

    public bool IsJoinPayment(MobileEntity guildmaster, ItemEntity gold)
    {
        return Guild is not null && gold.Amount == 500;
    }

    public bool Join(MobileEntity guildmaster, MobileEntity player, ItemEntity gold)
    {
        Joined.Add(gold);

        return Answer;
    }

    public bool Resign(MobileEntity guildmaster, MobileEntity player)
    {
        Resigned.Add(player);

        return Answer;
    }
}
