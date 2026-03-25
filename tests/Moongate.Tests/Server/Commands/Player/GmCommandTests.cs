using Moongate.Server.Commands.Player;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Types.Commands;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Ids;

namespace Moongate.Tests.Server.Commands.Player;

public sealed class GmCommandTests
{
    [Test]
    public async Task ExecuteCommandAsync_WhenArgumentsAreInvalid_ShouldPrintUsageAndSkipScriptCallback()
    {
        var scriptEngine = new GameEventScriptBridgeTestScriptEngineService();
        var command = new GmCommand(scriptEngine);
        var output = new List<string>();
        var context = new CommandSystemContext(
            "gm extra",
            ["extra"],
            CommandSourceType.InGame,
            7,
            (message, _) => output.Add(message),
            (Serial)0x00001234u
        );

        await command.ExecuteCommandAsync(context);

        Assert.Multiple(
            () =>
            {
                Assert.That(output, Has.Count.EqualTo(1));
                Assert.That(output[0], Is.EqualTo("Usage: .gm"));
                Assert.That(scriptEngine.LastCallbackName, Is.Null);
                Assert.That(scriptEngine.LastCallbackArgs, Is.Null);
            }
        );
    }

    [Test]
    public async Task ExecuteCommandAsync_WhenCharacterIsMissing_ShouldNotCallScriptCallback()
    {
        var scriptEngine = new GameEventScriptBridgeTestScriptEngineService();
        var command = new GmCommand(scriptEngine);
        var context = new CommandSystemContext(
            "gm",
            [],
            CommandSourceType.InGame,
            7,
            static (_, _) => { }
        );

        await command.ExecuteCommandAsync(context);

        Assert.That(scriptEngine.LastCallbackName, Is.Null);
        Assert.That(scriptEngine.LastCallbackArgs, Is.Null);
    }

    [Test]
    public async Task ExecuteCommandAsync_WhenCommandIsValid_ShouldCallLuaGmMenuFunctionWithSessionAndCharacter()
    {
        var scriptEngine = new GameEventScriptBridgeTestScriptEngineService();
        var command = new GmCommand(scriptEngine);
        var context = new CommandSystemContext(
            "gm",
            [],
            CommandSourceType.InGame,
            7,
            static (_, _) => { },
            (Serial)0x00001234u
        );

        await command.ExecuteCommandAsync(context);

        Assert.Multiple(
            () =>
            {
                Assert.That(scriptEngine.LastCallbackName, Is.EqualTo("on_gm_menu_request"));
                Assert.That(scriptEngine.LastCallbackArgs, Has.Length.EqualTo(2));
                Assert.That(scriptEngine.LastCallbackArgs![0], Is.EqualTo(7L));
                Assert.That(scriptEngine.LastCallbackArgs[1], Is.EqualTo((uint)0x00001234u));
            }
        );
    }
}
