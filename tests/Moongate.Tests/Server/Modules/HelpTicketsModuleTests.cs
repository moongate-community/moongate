using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Modules;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Modules;

public sealed class HelpTicketsModuleTests
{
    private sealed class HelpTicketsModuleServiceStub : IHelpTicketService
    {
        public HelpTicketCategory? LastCategory { get; private set; }

        public string? LastMessage { get; private set; }

        public long LastSessionId { get; private set; }

        public HelpTicketEntity? NextResult { get; set; }

        public Task<HelpTicketEntity?> CreateTicketAsync(
            long sessionId,
            HelpTicketCategory category,
            string message,
            CancellationToken cancellationToken = default
        )
        {
            _ = cancellationToken;
            LastSessionId = sessionId;
            LastCategory = category;
            LastMessage = message;
            return Task.FromResult(NextResult);
        }

        public Task<IReadOnlyList<HelpTicketEntity>> GetAllTicketsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<HelpTicketEntity>>([]);

        public Task<IReadOnlyList<HelpTicketEntity>> GetOpenTicketsForAccountAsync(
            Serial senderAccountId,
            CancellationToken cancellationToken = default
        )
            => Task.FromResult<IReadOnlyList<HelpTicketEntity>>([]);

        public Task StartAsync()
            => Task.CompletedTask;

        public Task StopAsync()
            => Task.CompletedTask;
    }

    [Test]
    public void Submit_WhenCategoryNameIsValid_ShouldDelegateToService()
    {
        var service = new HelpTicketsModuleServiceStub
        {
            NextResult = new HelpTicketEntity
            {
                Id = (Serial)(Serial.ItemOffset + 75),
                Category = HelpTicketCategory.Question,
                Message = "Need help",
                MapId = 0,
                Location = new Point3D(0, 0, 0),
                Status = HelpTicketStatus.Open,
                CreatedAtUtc = DateTime.UtcNow,
                LastUpdatedAtUtc = DateTime.UtcNow
            }
        };
        var module = new HelpTicketsModule(service);

        var result = module.Submit(123, "Question", "Need help");

        Assert.Multiple(
            () =>
            {
                Assert.That(result, Is.True);
                Assert.That(service.LastSessionId, Is.EqualTo(123));
                Assert.That(service.LastCategory, Is.EqualTo(HelpTicketCategory.Question));
                Assert.That(service.LastMessage, Is.EqualTo("Need help"));
            }
        );
    }

    [Test]
    public void Submit_WhenCategoryNameIsInvalid_ShouldReturnFalse()
    {
        var module = new HelpTicketsModule(new HelpTicketsModuleServiceStub());

        var result = module.Submit(123, "NotARealCategory", "Need help");

        Assert.That(result, Is.False);
    }
}
