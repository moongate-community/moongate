using Moongate.Core.Primitives;
using Moongate.Network.Packets.Outgoing;
using Moongate.Persistence.Entities;
using Moongate.Server.Data.Events;
using Moongate.Server.Interfaces.Accounts;
using Moongate.Server.Interfaces.Events;
using Moongate.UO.Data.Bodies;
using SquidStd.Core.Interfaces.Events;
using SquidStd.Persistence.Abstractions.Interfaces.Persistence;

namespace Moongate.Server.Subscribers;

/// <summary>
/// Opens the paperdoll (0x88) when a player double-clicks a humanoid. The common case is clicking
/// yourself: the client's paperdoll button sends a double click on your own serial. Mirrors ModernUO,
/// where the paperdoll is likewise an event subscriber rather than packet-handler logic.
/// </summary>
public sealed class PaperdollSubscriber : IEventSubscriberRegistration
{
    private readonly ISessionManager _sessions;
    private readonly IEntityStore<MobileEntity, Serial> _mobiles;

    public PaperdollSubscriber(ISessionManager sessions, IPersistenceService persistence)
    {
        _sessions = sessions;
        _mobiles = persistence.GetStore<MobileEntity, Serial>();
    }

    public void Subscribe(IEventBus eventBus)
        => eventBus.Subscribe<MobileDoubleClickEvent>(OnDoubleClick);

    private Task OnDoubleClick(MobileDoubleClickEvent message, CancellationToken cancellationToken)
    {
        if (!_sessions.TryGet(message.SessionId, out var session))
        {
            return Task.CompletedTask;
        }

        if (_mobiles.GetById(message.Serial) is { } beheld && Build(beheld, session.Character?.Id) is { } packet)
        {
            session.Send(packet);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Builds the paperdoll for <paramref name="beheld" />, or null when it has no paperdoll (a creature
    /// body). Lifting is only allowed on your own character, so it is on when the beheld is the beholder.
    /// </summary>
    public static PaperdollPacket? Build(MobileEntity beheld, Serial? beholderId)
    {
        if (!new Body(beheld.Body).IsHumanoid)
        {
            return null;
        }

        return new PaperdollPacket(beheld.Id, beheld.Name, beheld.Warmode, beholderId == beheld.Id);
    }
}
