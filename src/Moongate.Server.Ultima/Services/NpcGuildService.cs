using System.Globalization;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types.Guilds;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     The guilds of the trades, as ModernUO's <c>BaseGuildmaster</c> and its guilds.
/// </summary>
public sealed class NpcGuildService : INpcGuildService
{
    public const string GuildProp = "npc_guild";
    public const string JoinedProp = "npc_guild_joined";

    private const int JoinCost = 500;
    private const int ThievesStealing = 600;
    private const int ClilocWelcome = 1008054;
    private const int ClilocPrice = 1008052;
    private const int ClilocAlreadyMember = 501047;
    private const int ClilocOtherGuild = 501046;
    private const int ClilocNoThievesWithKills = 501050;
    private const int ClilocThievesNeedStealing = 501051;
    private const int ClilocNotMember = 501052;
    private const int ClilocJustJoined = 501053;
    private const int ClilocResigned = 501054;
    private static readonly TimeSpan QuitAfter = TimeSpan.FromDays(7);

    private readonly IMobileTemplateService _templates;
    private readonly IItemHandlingService _handling;
    private readonly ISpeechService _speech;
    private readonly ItemsConfig _items;
    private readonly TimeProvider _time;

    public NpcGuildService(
        IMobileTemplateService templates,
        IItemHandlingService handling,
        ISpeechService speech,
        ItemsConfig items,
        TimeProvider? time = null
    )
    {
        _templates = templates;
        _handling = handling;
        _speech = speech;
        _items = items;
        _time = time ?? TimeProvider.System;
    }

    public NpcGuildType? Of(MobileEntity guildmaster)
    {
        ArgumentNullException.ThrowIfNull(guildmaster);

        return guildmaster.IsNpc && guildmaster.TemplateId is { } id && _templates.TryGet(id, out var template)
            ? template.NpcGuild
            : null;
    }

    public NpcGuildType? MemberOf(MobileEntity player)
    {
        ArgumentNullException.ThrowIfNull(player);

        return player.TryGetProp<string>(GuildProp, out var name) && Enum.TryParse<NpcGuildType>(name, true, out var guild)
            ? guild
            : null;
    }

    public bool Quote(MobileEntity guildmaster, MobileEntity player)
    {
        ArgumentNullException.ThrowIfNull(player);

        if (Of(guildmaster) is not { } guild || player.IsNpc || player.IsDead || !CanJoin(guildmaster, player, guild))
        {
            return false;
        }

        _speech.SayCliloc(guildmaster, ClilocPrice, "", $" {JoinCost}");

        return true;
    }

    public bool Join(MobileEntity guildmaster, MobileEntity player, ItemEntity gold)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(gold);

        if (Of(guildmaster) is not { } guild ||
            player.IsNpc ||
            player.IsDead ||
            gold.TemplateId != _items.GoldTemplate ||
            gold.Amount != JoinCost ||
            !CanJoin(guildmaster, player, guild) ||
            !_handling.Consume(gold, JoinCost))
        {
            return false;
        }

        player.SetProp(GuildProp, guild.ToString());
        player.SetProp(JoinedProp, _time.GetUtcNow().UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
        _speech.SayCliloc(guildmaster, ClilocWelcome);

        return true;
    }

    public bool Resign(MobileEntity guildmaster, MobileEntity player)
    {
        ArgumentNullException.ThrowIfNull(player);

        if (Of(guildmaster) is not { } guild || player.IsNpc || player.IsDead)
        {
            return false;
        }

        if (MemberOf(player) != guild)
        {
            _speech.SayCliloc(guildmaster, ClilocNotMember);

            return false;
        }

        if (JoinedAt(player) + QuitAfter > _time.GetUtcNow().UtcDateTime)
        {
            _speech.SayCliloc(guildmaster, ClilocJustJoined);

            return false;
        }

        player.RemoveProp(GuildProp);
        player.RemoveProp(JoinedProp);
        _speech.SayCliloc(guildmaster, ClilocResigned);

        return true;
    }

    private static DateTime JoinedAt(MobileEntity player)
    {
        return player.TryGetProp<string>(JoinedProp, out var text) &&
               DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var joined)
            ? joined
            : DateTime.MinValue;
    }

    // Whether the player may join the guild now; the guildmaster tells why not.
    private bool CanJoin(MobileEntity guildmaster, MobileEntity player, NpcGuildType guild)
    {
        if (MemberOf(player) is { } current)
        {
            _speech.SayCliloc(guildmaster, current == guild ? ClilocAlreadyMember : ClilocOtherGuild);

            return false;
        }

        if (guild != NpcGuildType.Thieves)
        {
            return true;
        }

        if (player.Kills > 0)
        {
            _speech.SayCliloc(guildmaster, ClilocNoThievesWithKills);

            return false;
        }

        if ((player.Skills.FirstOrDefault(known => known.Skill == SkillType.Stealing)?.Base ?? 0) < ThievesStealing)
        {
            _speech.SayCliloc(guildmaster, ClilocThievesNeedStealing);

            return false;
        }

        return true;
    }
}
