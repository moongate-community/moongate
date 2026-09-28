using Moongate.Core.Primitives;
using Moongate.Core.Types.Geometry;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Outgoing.Login;
using Moongate.Network.Packets.Types.Login;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Packets;
using Moongate.Server.Ultima.Data.Characters;
using Moongate.Server.Ultima.Data.Events;
using Moongate.Server.Ultima.Data.Maps;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Server.Ultima.Packets.Characters;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Ultima.Primitives;
using Moongate.Ultima.Types;
using Serilog;

namespace Moongate.Server.Ultima.Handlers.Characters;

/// <summary>
///     Brings the character the client chose (0x5D) into the world: it sends the enter-world sequence the other
///     emulators send (ModernUO's order), marks the character in the world and publishes
///     <see cref="CharacterEnteredWorldEvent" /> after the login completes.
/// </summary>
public sealed class PlayCharacterPacketHandler : IAsyncPacketHandler<PlayCharacterPacket>
{
    private const int StatCap = 225;
    private const int FollowersMax = 5;

    private readonly ILogger _logger = Log.ForContext<PlayCharacterPacketHandler>();
    private readonly ICharacterService _characters;
    private readonly IMobileService _mobiles;
    private readonly IDataLoaderService _data;
    private readonly IMoongateEventBus _events;
    private readonly ISessionService _sessions;

    public PlayCharacterPacketHandler(
        ICharacterService characters,
        IMobileService mobiles,
        IDataLoaderService data,
        IMoongateEventBus events,
        ISessionService sessions
    )
    {
        _characters = characters;
        _mobiles = mobiles;
        _data = data;
        _events = events;
        _sessions = sessions;
    }

    public async ValueTask HandleAsync(
        PacketContext context,
        PlayCharacterPacket packet,
        CancellationToken cancellationToken
    )
    {
        var accountId = Serial.Zero;
        await context.RunOnGameLoopAsync(session => accountId = session.AccountId, cancellationToken)
                     .ConfigureAwait(false);

        if (!accountId.IsValid)
        {
            _logger.Warning("Play character from session {SessionId} without an account", context.SessionId);
            await RefuseAsync(context, PopupMessageType.CouldNotAttach, cancellationToken).ConfigureAwait(false);

            return;
        }

        CharacterForPlay? play;

        try
        {
            play = await _characters.GetForPlayAsync(accountId, packet.CharacterIndex, cancellationToken)
                                    .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.Error(exception, "Loading the character to play for account {AccountId} failed", accountId);
            await RefuseAsync(context, PopupMessageType.CouldNotAttach, cancellationToken).ConfigureAwait(false);

            return;
        }

        if (play is null)
        {
            _logger.Information(
                "Account {AccountId} chose list position {Index}, which holds no character",
                accountId,
                packet.CharacterIndex
            );
            await RefuseAsync(context, PopupMessageType.CharacterDoesNotExist, cancellationToken).ConfigureAwait(false);

            return;
        }

        var character = play.Character;
        var admitted = false;
        await context.RunOnGameLoopAsync(
                         session =>
                         {
                             // As ModernUO: one character per account in the world at a time.
                             if (_sessions.GetAll().Any(other => other.SessionId != session.SessionId &&
                                                                 other.AccountId == accountId &&
                                                                 other.CharacterId.IsValid))
                             {
                                 return;
                             }

                             session.Set(SessionKeys.CharacterId, character.Id);
                             admitted = true;
                         },
                         cancellationToken
                     )
                     .ConfigureAwait(false);

        if (!admitted)
        {
            _logger.Information("Account {AccountId} already has a character in the world", accountId);
            await RefuseAsync(context, PopupMessageType.CharacterInWorld, cancellationToken).ConfigureAwait(false);

            return;
        }

        _mobiles.EnterWorld(character.Id);

        foreach (var outgoing in EnterWorldSequence(play))
        {
            context.TrySend(outgoing);
        }

        _logger.Information(
            "Session {SessionId}: account {AccountId} entered the world with {Character}",
            context.SessionId,
            accountId,
            character
        );

        // As in every emulator, the login hook runs once the client knows the login is complete.
        await _events.PublishAsync(new CharacterEnteredWorldEvent(character), CancellationToken.None).ConfigureAwait(false);
    }

    private IEnumerable<IOutgoingPacket> EnterWorldSequence(CharacterForPlay play)
    {
        var character = play.Character;
        var map = _data.GetEntities<MapContent>().FirstOrDefault(content => content.Map == character.Map);
        var body = new Body((ushort)character.Body);
        var flags = character.Gender == GenderType.Female ? MobileFlagsType.Female : MobileFlagsType.None;
        const DirectionType direction = DirectionType.South;

        yield return new LoginConfirmPacket(
            character.Id,
            body,
            character.Location,
            direction,
            map?.Size.X ?? 7168,
            map?.Size.Y ?? 4096
        );
        yield return new MapChangePacket(character.Map);
        yield return new SeasonChangePacket(map?.Season ?? SeasonType.Summer, false);
        yield return new GlobalLightLevelPacket(0);
        yield return new PersonalLightLevelPacket(character.Id, 0);
        yield return new MobileUpdatePacket(character.Id, body, character.SkinHue, flags, character.Location, direction);
        yield return new MobileIncomingPacket(
            character.Id,
            body,
            character.Location,
            direction,
            character.SkinHue,
            flags,
            character.Notoriety ?? NotorietyType.Innocent,
            Equipment(play)
        );
        yield return new MobileStatusPacket(Status(character));
        yield return new WarModePacket(false);
        yield return new LoginCompletePacket();
        yield return new CurrentTimePacket(TimeOnly.FromDateTime(DateTime.UtcNow));
    }

    /// <summary>
    ///     The worn items, one per layer, then the hair and beard the mobile has as virtual items.
    /// </summary>
    private List<MobileEquipmentEntry> Equipment(CharacterForPlay play)
    {
        var character = play.Character;
        var entries = play.Equipment
                          .Where(item => item.Layer is not null)
                          .GroupBy(item => item.Layer!.Value)
                          .Select(group => group.First())
                          .Select(item => new MobileEquipmentEntry(item.Id, item.ItemId, item.Layer!.Value, item.Hue))
                          .ToList();
        var layers = entries.Select(entry => entry.Layer).ToHashSet();

        if (character.HairStyle > 0 && layers.Add(LayerType.Hair))
        {
            entries.Add(new(_mobiles.HairSerial(character.Id), character.HairStyle, LayerType.Hair, character.HairHue));
        }

        if (character.BeardStyle > 0 && layers.Add(LayerType.FacialHair))
        {
            entries.Add(
                new(_mobiles.BeardSerial(character.Id), character.BeardStyle, LayerType.FacialHair, character.BeardHue)
            );
        }

        return entries;
    }

    private static MobileStatusInfo Status(MobileEntity character)
    {
        return new()
        {
            Serial = character.Id,
            Name = character.Name,
            Hits = character.Hits,
            HitsMax = character.HitsMax,
            Female = character.Gender == GenderType.Female,
            Strength = character.Strength,
            Dexterity = character.Dexterity,
            Intelligence = character.Intelligence,
            Stamina = character.Stamina,
            StaminaMax = character.StaminaMax,
            Mana = character.Mana,
            ManaMax = character.ManaMax,
            PhysicalResistance = character.ResistPhysical,
            Race = character.Race,
            StatCap = StatCap,
            FollowersMax = FollowersMax,
            FireResistance = character.ResistFire,
            ColdResistance = character.ResistCold,
            PoisonResistance = character.ResistPoison,
            EnergyResistance = character.ResistEnergy
        };
    }

    private static async Task RefuseAsync(PacketContext context, PopupMessageType popup, CancellationToken cancellationToken)
    {
        await context.SendAndDisconnectAsync(new PopupMessagePacket(popup), cancellationToken).ConfigureAwait(false);
    }
}
