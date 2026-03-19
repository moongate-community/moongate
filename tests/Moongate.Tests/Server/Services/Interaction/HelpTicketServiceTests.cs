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

    [Test]
    public async Task GetTicketsForAdminAsync_WhenFiltersAreApplied_ShouldReturnPagedMatchesOrderedByNewestFirst()
    {
        using var tempDirectory = new TempDirectory();
        using var persistence = new TestPersistenceService(tempDirectory.Path);
        await persistence.UnitOfWork.InitializeAsync();

        var sessionService = new FakeGameNetworkSessionService();
        var eventBus = new RecordingEventBus();
        var service = new HelpTicketService(sessionService, persistence, eventBus);

        var oldest = CreateExistingTicket(
            (Serial)(Serial.ItemOffset + 1),
            (Serial)7,
            HelpTicketCategory.Question,
            HelpTicketStatus.Open,
            new DateTime(2026, 3, 19, 10, 0, 0, DateTimeKind.Utc)
        );
        var filteredNewest = CreateExistingTicket(
            (Serial)(Serial.ItemOffset + 2),
            (Serial)7,
            HelpTicketCategory.Question,
            HelpTicketStatus.Open,
            new DateTime(2026, 3, 19, 12, 0, 0, DateTimeKind.Utc)
        );
        filteredNewest.AssignedToAccountId = (Serial)0x00000011u;
        filteredNewest.AssignedAtUtc = filteredNewest.CreatedAtUtc;
        var otherCategory = CreateExistingTicket(
            (Serial)(Serial.ItemOffset + 3),
            (Serial)7,
            HelpTicketCategory.Bug,
            HelpTicketStatus.Open,
            new DateTime(2026, 3, 19, 11, 0, 0, DateTimeKind.Utc)
        );
        var otherStatus = CreateExistingTicket(
            (Serial)(Serial.ItemOffset + 4),
            (Serial)7,
            HelpTicketCategory.Question,
            HelpTicketStatus.Closed,
            new DateTime(2026, 3, 19, 13, 0, 0, DateTimeKind.Utc)
        );

        await persistence.UnitOfWork.HelpTickets.UpsertAsync(oldest);
        await persistence.UnitOfWork.HelpTickets.UpsertAsync(filteredNewest);
        await persistence.UnitOfWork.HelpTickets.UpsertAsync(otherCategory);
        await persistence.UnitOfWork.HelpTickets.UpsertAsync(otherStatus);

        var (items, totalCount) = await service.GetTicketsForAdminAsync(
            page: 1,
            pageSize: 10,
            status: HelpTicketStatus.Open,
            category: HelpTicketCategory.Question,
            assignedToAccountId: null
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(totalCount, Is.EqualTo(2));
                Assert.That(items.Select(ticket => ticket.Id).ToArray(), Is.EqualTo(new[] { filteredNewest.Id, oldest.Id }));
            }
        );
    }

    [Test]
    public async Task GetTicketsForAdminAsync_WhenAssignedToFilterIsApplied_ShouldReturnOnlyMatchingTickets()
    {
        using var tempDirectory = new TempDirectory();
        using var persistence = new TestPersistenceService(tempDirectory.Path);
        await persistence.UnitOfWork.InitializeAsync();

        var sessionService = new FakeGameNetworkSessionService();
        var eventBus = new RecordingEventBus();
        var service = new HelpTicketService(sessionService, persistence, eventBus);

        var mine = CreateExistingTicket(
            (Serial)(Serial.ItemOffset + 11),
            (Serial)7,
            HelpTicketCategory.Question,
            HelpTicketStatus.Assigned,
            new DateTime(2026, 3, 19, 12, 0, 0, DateTimeKind.Utc)
        );
        mine.AssignedToAccountId = (Serial)0x00000077u;
        mine.AssignedAtUtc = mine.CreatedAtUtc;

        var someoneElse = CreateExistingTicket(
            (Serial)(Serial.ItemOffset + 12),
            (Serial)7,
            HelpTicketCategory.Question,
            HelpTicketStatus.Assigned,
            new DateTime(2026, 3, 19, 12, 30, 0, DateTimeKind.Utc)
        );
        someoneElse.AssignedToAccountId = (Serial)0x00000078u;
        someoneElse.AssignedAtUtc = someoneElse.CreatedAtUtc;

        await persistence.UnitOfWork.HelpTickets.UpsertAsync(mine);
        await persistence.UnitOfWork.HelpTickets.UpsertAsync(someoneElse);

        var (items, totalCount) = await service.GetTicketsForAdminAsync(
            page: 1,
            pageSize: 10,
            status: null,
            category: null,
            assignedToAccountId: (Serial)0x00000077u
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(totalCount, Is.EqualTo(1));
                Assert.That(items.Select(ticket => ticket.Id).ToArray(), Is.EqualTo(new[] { mine.Id }));
            }
        );
    }

    [Test]
    public async Task GetTicketByIdAsync_WhenTicketExists_ShouldReturnTicket()
    {
        using var tempDirectory = new TempDirectory();
        using var persistence = new TestPersistenceService(tempDirectory.Path);
        await persistence.UnitOfWork.InitializeAsync();

        var sessionService = new FakeGameNetworkSessionService();
        var eventBus = new RecordingEventBus();
        var service = new HelpTicketService(sessionService, persistence, eventBus);

        var expected = CreateExistingTicket(
            (Serial)(Serial.ItemOffset + 21),
            (Serial)7,
            HelpTicketCategory.Bug,
            HelpTicketStatus.Open,
            new DateTime(2026, 3, 19, 9, 0, 0, DateTimeKind.Utc)
        );

        await persistence.UnitOfWork.HelpTickets.UpsertAsync(expected);

        var loaded = await service.GetTicketByIdAsync(expected.Id);

        Assert.That(loaded?.Id, Is.EqualTo(expected.Id));
    }

    [Test]
    public async Task GetTicketByIdAsync_WhenTicketDoesNotExist_ShouldReturnNull()
    {
        using var tempDirectory = new TempDirectory();
        using var persistence = new TestPersistenceService(tempDirectory.Path);
        await persistence.UnitOfWork.InitializeAsync();

        var sessionService = new FakeGameNetworkSessionService();
        var eventBus = new RecordingEventBus();
        var service = new HelpTicketService(sessionService, persistence, eventBus);

        var loaded = await service.GetTicketByIdAsync((Serial)(Serial.ItemOffset + 404));

        Assert.That(loaded, Is.Null);
    }

    [Test]
    public async Task AssignToAccountAsync_WhenTicketExists_ShouldSetAssignmentFieldsAndAssignedStatus()
    {
        using var tempDirectory = new TempDirectory();
        using var persistence = new TestPersistenceService(tempDirectory.Path);
        await persistence.UnitOfWork.InitializeAsync();

        var sessionService = new FakeGameNetworkSessionService();
        var eventBus = new RecordingEventBus();
        var service = new HelpTicketService(sessionService, persistence, eventBus);

        var ticket = CreateExistingTicket(
            (Serial)(Serial.ItemOffset + 31),
            (Serial)7,
            HelpTicketCategory.Question,
            HelpTicketStatus.Open,
            new DateTime(2026, 3, 19, 9, 0, 0, DateTimeKind.Utc)
        );

        await persistence.UnitOfWork.HelpTickets.UpsertAsync(ticket);

        var updated = await service.AssignToAccountAsync(ticket.Id, (Serial)0x00000077u, (Serial)0x00000088u);

        Assert.Multiple(
            () =>
            {
                Assert.That(updated, Is.Not.Null);
                Assert.That(updated!.Status, Is.EqualTo(HelpTicketStatus.Assigned));
                Assert.That(updated.AssignedToAccountId, Is.EqualTo((Serial)0x00000077u));
                Assert.That(updated.AssignedToCharacterId, Is.EqualTo((Serial)0x00000088u));
                Assert.That(updated.AssignedAtUtc, Is.Not.Null);
                Assert.That(updated.LastUpdatedAtUtc, Is.GreaterThanOrEqualTo(ticket.CreatedAtUtc));
            }
        );
    }

    [Test]
    public async Task UpdateStatusAsync_WhenClosed_ShouldSetClosedAtUtc()
    {
        using var tempDirectory = new TempDirectory();
        using var persistence = new TestPersistenceService(tempDirectory.Path);
        await persistence.UnitOfWork.InitializeAsync();

        var sessionService = new FakeGameNetworkSessionService();
        var eventBus = new RecordingEventBus();
        var service = new HelpTicketService(sessionService, persistence, eventBus);

        var ticket = CreateExistingTicket(
            (Serial)(Serial.ItemOffset + 41),
            (Serial)7,
            HelpTicketCategory.Bug,
            HelpTicketStatus.Assigned,
            new DateTime(2026, 3, 19, 9, 0, 0, DateTimeKind.Utc)
        );
        ticket.AssignedToAccountId = (Serial)0x00000077u;
        ticket.AssignedToCharacterId = (Serial)0x00000088u;
        ticket.AssignedAtUtc = ticket.CreatedAtUtc;

        await persistence.UnitOfWork.HelpTickets.UpsertAsync(ticket);

        var updated = await service.UpdateStatusAsync(ticket.Id, HelpTicketStatus.Closed);

        Assert.Multiple(
            () =>
            {
                Assert.That(updated, Is.Not.Null);
                Assert.That(updated!.Status, Is.EqualTo(HelpTicketStatus.Closed));
                Assert.That(updated.ClosedAtUtc, Is.Not.Null);
                Assert.That(updated.AssignedToAccountId, Is.EqualTo((Serial)0x00000077u));
            }
        );
    }

    [Test]
    public async Task UpdateStatusAsync_WhenReopened_ShouldPreserveAssignmentHistory()
    {
        using var tempDirectory = new TempDirectory();
        using var persistence = new TestPersistenceService(tempDirectory.Path);
        await persistence.UnitOfWork.InitializeAsync();

        var sessionService = new FakeGameNetworkSessionService();
        var eventBus = new RecordingEventBus();
        var service = new HelpTicketService(sessionService, persistence, eventBus);

        var ticket = CreateExistingTicket(
            (Serial)(Serial.ItemOffset + 51),
            (Serial)7,
            HelpTicketCategory.Bug,
            HelpTicketStatus.Closed,
            new DateTime(2026, 3, 19, 9, 0, 0, DateTimeKind.Utc)
        );
        ticket.AssignedToAccountId = (Serial)0x00000077u;
        ticket.AssignedToCharacterId = (Serial)0x00000088u;
        ticket.AssignedAtUtc = ticket.CreatedAtUtc;
        ticket.ClosedAtUtc = ticket.CreatedAtUtc.AddMinutes(3);

        await persistence.UnitOfWork.HelpTickets.UpsertAsync(ticket);

        var updated = await service.UpdateStatusAsync(ticket.Id, HelpTicketStatus.Open);

        Assert.Multiple(
            () =>
            {
                Assert.That(updated, Is.Not.Null);
                Assert.That(updated!.Status, Is.EqualTo(HelpTicketStatus.Open));
                Assert.That(updated.AssignedToAccountId, Is.EqualTo((Serial)0x00000077u));
                Assert.That(updated.AssignedToCharacterId, Is.EqualTo((Serial)0x00000088u));
                Assert.That(updated.AssignedAtUtc, Is.Not.Null);
            }
        );
    }

    private static HelpTicketEntity CreateExistingTicket(
        Serial id,
        Serial senderAccountId,
        HelpTicketCategory category,
        HelpTicketStatus status,
        DateTime createdAtUtc
    )
        => new()
        {
            Id = id,
            SenderCharacterId = (Serial)0x00000042u,
            SenderAccountId = senderAccountId,
            Category = category,
            Message = $"ticket-{id.Value}",
            MapId = 0,
            Location = new Point3D(1443, 1692, 0),
            Status = status,
            CreatedAtUtc = createdAtUtc,
            LastUpdatedAtUtc = createdAtUtc
        };
}
