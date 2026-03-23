using Moongate.Server.Data.Events.Combat;
using Moongate.Server.Data.Events.Connections;
using Moongate.Server.Data.Events.Help;
using Moongate.Server.Data.Events.Scheduling;
using Moongate.Server.Data.Scripting;
using Moongate.Server.Services.Events;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Services.Events;

public class GameEventScriptBridgeServiceTests
{
    [Test]
    public async Task HandleAsync_ShouldExecuteAggressiveActionCallback_WithSnakeCaseEventName()
    {
        var scriptEngine = new GameEventScriptBridgeTestScriptEngineService();
        var service = new GameEventScriptBridgeService(scriptEngine);
        var attacker = new UOMobileEntity { Id = (Serial)0x00000002u, MapId = 0, Location = new(100, 100, 0) };
        var defender = new UOMobileEntity { Id = (Serial)0x00000003u, MapId = 0, Location = new(101, 100, 0) };
        var gameEvent = new AggressiveActionEvent(
            attacker.Id,
            defender.Id,
            attacker.MapId,
            attacker.Location,
            attacker,
            defender
        );

        await service.HandleAsync(gameEvent);

        Assert.That(scriptEngine.LastCallbackName, Is.EqualTo("on_aggressive_action"));
    }

    [Test]
    public async Task HandleAsync_ShouldExecuteScheduledEventCallback_WithSnakeCaseEventName()
    {
        var scriptEngine = new GameEventScriptBridgeTestScriptEngineService();
        var service = new GameEventScriptBridgeService(scriptEngine);
        var scheduledAtUtc = new DateTime(2026, 03, 19, 9, 0, 0, DateTimeKind.Utc);
        var firedAtUtc = scheduledAtUtc.AddSeconds(2);
        var gameEvent = new ScheduledEventTriggeredEvent(
            "town_crier_morning",
            "town_crier_announcement",
            scheduledAtUtc,
            firedAtUtc,
            ScheduledRecurrenceType.Daily
        );

        await service.HandleAsync(gameEvent);

        Assert.That(scriptEngine.LastCallbackName, Is.EqualTo("on_scheduled_event"));
        Assert.That(scriptEngine.LastCallbackArgs, Has.Length.EqualTo(1));
        Assert.That(scriptEngine.LastCallbackArgs![0], Is.EqualTo(gameEvent));
    }

    [Test]
    public async Task HandleAsync_ShouldExecuteScriptCallback_WithSnakeCaseEventName()
    {
        var scriptEngine = new GameEventScriptBridgeTestScriptEngineService();
        var service = new GameEventScriptBridgeService(scriptEngine);
        var gameEvent = new PlayerConnectedEvent(42, "127.0.0.1:2593", 100);

        await service.HandleAsync(gameEvent);

        Assert.That(scriptEngine.LastCallbackName, Is.EqualTo("on_player_connected"));
        Assert.That(scriptEngine.LastCallbackArgs, Has.Length.EqualTo(1));
        Assert.That(scriptEngine.LastCallbackArgs![0], Is.EqualTo(gameEvent));
    }

    [Test]
    public async Task HandleAsync_ShouldExecuteTicketOpenedCallback_WithSnakeCaseEventName()
    {
        var scriptEngine = new GameEventScriptBridgeTestScriptEngineService();
        var service = new GameEventScriptBridgeService(scriptEngine);
        var gameEvent = new TicketOpenedEvent(
            (Serial)(Serial.ItemOffset + 75),
            (Serial)0x00000042u,
            (Serial)0x00000010u,
            HelpTicketCategory.Question,
            "I am stuck behind the innkeeper counter.",
            0,
            new(1443, 1692, 0)
        );

        await service.HandleAsync(gameEvent);

        Assert.That(scriptEngine.LastCallbackName, Is.EqualTo("on_ticket_opened"));
        Assert.That(scriptEngine.LastCallbackArgs, Has.Length.EqualTo(1));
        Assert.That(scriptEngine.LastCallbackArgs![0], Is.EqualTo(gameEvent));
    }

    [Test]
    public void StartAsync_ShouldCompleteWithoutErrors()
    {
        var scriptEngine = new GameEventScriptBridgeTestScriptEngineService();
        var service = new GameEventScriptBridgeService(scriptEngine);

        Assert.DoesNotThrowAsync(async () => await service.StartAsync());
    }

    [Test]
    public void StopAsync_ShouldCompleteWithoutErrors()
    {
        var scriptEngine = new GameEventScriptBridgeTestScriptEngineService();
        var service = new GameEventScriptBridgeService(scriptEngine);

        Assert.DoesNotThrowAsync(async () => await service.StopAsync());
    }
}
