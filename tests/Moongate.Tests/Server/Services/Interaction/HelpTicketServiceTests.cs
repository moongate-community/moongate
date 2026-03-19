using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Persistence.Interfaces.Persistence;
using Moongate.Persistence.Services.Persistence;
using Moongate.Server.Data.Events.Base;
using Moongate.Server.Data.Events.Help;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Persistence;
using Moongate.Server.Services.Interaction;
using Moongate.Tests.Server.Services.Spatial;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Services.Interaction;

public sealed class HelpTicketServiceTests
{
    private sealed class RecordingEventBus : IGameEventBusService
    {
        public List<object> Events { get; } = [];

        public ValueTask PublishAsync<TEvent>(TEvent gameEvent, CancellationToken cancellationToken = default)
            where TEvent : IGameEvent
        {
            _ = cancellationToken;
            Events.Add(gameEvent!);
            return ValueTask.CompletedTask;
        }

        public void RegisterListener<TEvent>(IGameEventListener<TEvent> listener) where TEvent : IGameEvent { }
    }

    private sealed class TestPersistenceService : IPersistenceService
    {
        public TestPersistenceService(string directory)
        {
            UnitOfWork = new PersistenceUnitOfWork(
                new(
                    Path.Combine(directory, "world.snapshot.bin"),
                    Path.Combine(directory, "world.journal.bin")
                )
            );
        }

        public IPersistenceUnitOfWork UnitOfWork { get; }

        public void Dispose()
        {
            if (UnitOfWork is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        public Task SaveAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task StartAsync()
            => Task.CompletedTask;

        public Task StopAsync()
            => Task.CompletedTask;
    }

    [Test]
    public async Task CreateTicketAsync_WhenSessionIsValid_ShouldPersistTicketAndPublishOpenedEvent()
    {
        using var tempDirectory = new TempDirectory();
        using var persistence = new TestPersistenceService(tempDirectory.Path);
        await persistence.UnitOfWork.InitializeAsync();

        var sessionService = new FakeGameNetworkSessionService();
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            AccountId = (Serial)0x00000010u,
            CharacterId = (Serial)0x00000042u,
            Character = new UOMobileEntity
            {
                Id = (Serial)0x00000042u,
                AccountId = (Serial)0x00000010u,
                MapId = 0,
                Location = new Point3D(1443, 1692, 0)
            }
        };
        sessionService.Add(session);

        var eventBus = new RecordingEventBus();
        var service = new HelpTicketService(sessionService, persistence, eventBus);

        var ticket = await service.CreateTicketAsync(
            session.SessionId,
            HelpTicketCategory.Question,
            "  I am stuck behind the innkeeper counter.  "
        );
        var persisted = ticket is null ? null : await persistence.UnitOfWork.HelpTickets.GetByIdAsync(ticket.Id);

        Assert.Multiple(
            () =>
            {
                Assert.That(ticket, Is.Not.Null);
                Assert.That(ticket!.Status, Is.EqualTo(HelpTicketStatus.Open));
                Assert.That(ticket.Message, Is.EqualTo("I am stuck behind the innkeeper counter."));
                Assert.That(ticket.SenderAccountId, Is.EqualTo((Serial)0x00000010u));
                Assert.That(ticket.SenderCharacterId, Is.EqualTo((Serial)0x00000042u));
                Assert.That(ticket.Location, Is.EqualTo(new Point3D(1443, 1692, 0)));
                Assert.That(persisted, Is.Not.Null);
                Assert.That(eventBus.Events.OfType<TicketOpenedEvent>().Any(e => e.TicketId == ticket.Id), Is.True);
            }
        );
    }
}
