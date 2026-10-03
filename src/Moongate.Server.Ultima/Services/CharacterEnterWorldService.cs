using Moongate.Core.Primitives;
using Moongate.Core.Types.Geometry;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Outgoing.Login;
using Moongate.Network.Packets.Types.Login;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Packets;
using Moongate.Server.Ultima.Data.Characters;
using Moongate.Server.Ultima.Data.Events;
using Moongate.Server.Ultima.Data.Maps;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Server.Ultima.Interfaces.Motd;
using Moongate.Server.Ultima.Packets.Characters;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Ultima.Primitives;
using Moongate.Ultima.Types;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Brings a character into the world: it sends the enter-world sequence the other emulators send (ModernUO's
///     order), marks the character in the world and publishes <see cref="CharacterEnteredWorldEvent" /> after the login
///     completes. Used when the client chooses a character (0x5D) and right after it creates one (0xF8, 0x8D).
/// </summary>
public sealed class CharacterEnterWorldService : ICharacterEnterWorldService
{
    private readonly ILogger _logger = Log.ForContext<CharacterEnterWorldService>();
    private readonly IMobileService _mobiles;
    private readonly IItemService _items;
    private readonly IDataLoaderService _data;
    private readonly IMoongateEventBus _events;
    private readonly ISessionService _sessions;
    private readonly IMotdService _motd;
    private readonly ILightService? _light;
    private readonly ISeasonService? _seasons;
    private readonly IWorldViewService _view;

    public CharacterEnterWorldService(
        IMobileService mobiles,
        IItemService items,
        IDataLoaderService data,
        IMoongateEventBus events,
        ISessionService sessions,
        IWorldViewService view,
        IMotdService motd,
        ILightService? light = null,
        ISeasonService? seasons = null
    )
    {
        _light = light;
        _seasons = seasons;
        _mobiles = mobiles;
        _items = items;
        _data = data;
        _events = events;
        _sessions = sessions;
        _motd = motd;
        _view = view;
    }

    public bool CanEnter(GameSession session)
    {
        // A second character on the same session would replace the first and leave it in the world for ever.
        return !session.CharacterId.IsValid &&
               !_sessions.GetAll()
                         .Any(
                             other => other.SessionId != session.SessionId &&
                                      other.AccountId == session.AccountId &&
                                      other.CharacterId.IsValid
                         );
    }

    public async Task EnterAsync(
        PacketContext context,
        Serial accountId,
        CharacterForPlay play,
        CancellationToken cancellationToken
    )
    {
        var character = play.Character;
        var admitted = false;
        await context.RunOnGameLoopAsync(
                session =>
                {
                    if (!CanEnter(session))
                    {
                        return;
                    }

                    // Together on the loop: a session retirement then always finds the character live.
                    session.Set(SessionKeys.CharacterId, character.Id);
                    _mobiles.EnterWorld(character);

                    // Its rows are as its last save left them: what another player took or merged since stays out, and
                    // is not shown on the character either.
                    var added = _items.AddLoaded(play.Equipment.Concat(play.Contents)).ToHashSet();
                    play = play with
                    {
                        Equipment = play.Equipment.Where(added.Contains).ToList(),
                        Contents = play.Contents.Where(added.Contains).ToList()
                    };
                    admitted = true;
                },
                cancellationToken
            );

        if (!admitted)
        {
            _logger.Information("Account {AccountId} already has a character in the world", accountId);
            await RefuseAsync(context, PopupMessageType.CharacterInWorld, cancellationToken);

            return;
        }

        foreach (var outgoing in EnterWorldSequence(play))
        {
            context.TrySend(outgoing);
        }

        // After the sequence: the client must know where it stands before it is shown the others.
        if (!await context.RunOnGameLoopAsync(
                session => _view.Entered(character, session.SessionId, session.ClientVersion, session.AccountType),
                cancellationToken
            ))
        {
            // The session closed during the sequence: its leave already ran, so the login never completed.
            _logger.Information("Session {SessionId} closed while {Character} entered the world", context.SessionId, character);

            return;
        }

        _logger.Information(
            "Session {SessionId}: account {AccountId} entered the world with {Character}",
            context.SessionId,
            accountId,
            character
        );

        await _motd.SendAsync(context, character, cancellationToken);

        // As in every emulator, the login hook runs once the client knows the login is complete.
        await _events.PublishAsync(new CharacterEnteredWorldEvent(character), CancellationToken.None);
    }

    private IEnumerable<IOutgoingPacket> EnterWorldSequence(CharacterForPlay play)
    {
        var character = play.Character;
        var map = _data.GetEntities<MapContent>().FirstOrDefault(content => content.Map == character.Map);
        var body = new Body((ushort)character.Body);
        var flags = _mobiles.GetFlags(character);
        var direction = character.Direction;

        yield return new LoginConfirmPacket(
            character.Id,
            body,
            character.Location,
            direction,
            map?.Size.X ?? 7168,
            map?.Size.Y ?? 4096
        );
        yield return new MapChangePacket(character.Map);
        yield return new SeasonChangePacket(_seasons?.SeasonOnLogin(character) ?? map?.Season ?? SeasonType.Summer, false);
        yield return new GlobalLightLevelPacket(_light?.LevelOnLogin(character) ?? 0);
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
            _mobiles.GetEquipment(character, play.Equipment)
        );
        yield return new MobileStatusPacket(_mobiles.GetStatus(character));
        yield return new WarModePacket(false);
        yield return new LoginCompletePacket();
        yield return new CurrentTimePacket(TimeOnly.FromDateTime(DateTime.UtcNow));
    }

    private static async Task RefuseAsync(PacketContext context, PopupMessageType popup, CancellationToken cancellationToken)
    {
        await context.SendAndDisconnectAsync(new PopupMessagePacket(popup), cancellationToken);
    }
}
