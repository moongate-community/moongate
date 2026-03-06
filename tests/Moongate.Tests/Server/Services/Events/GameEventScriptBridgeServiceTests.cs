using Moongate.Server.Data.Events.Connections;
using Moongate.Server.Services.Events;
using Moongate.Tests.Server.Support;

namespace Moongate.Tests.Server.Services.Events;

public class GameEventScriptBridgeServiceTests
{
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
