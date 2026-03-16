using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Services.Console;
using Moongate.Server.Services.Events;
using Moongate.Tests.Server.Support;
using Serilog.Events;

namespace Moongate.Tests.Server.Services.Console;

public sealed class ConsoleCommandServiceTests
{
    private sealed class ThrowingConsoleUiService : IConsoleUiService
    {
        public bool IsInteractive => true;

        public bool IsInputLocked => true;

        public char UnlockCharacter => '*';

        public void LockInput()
            => throw new InvalidOperationException("Debugger console does not support cursor positioning.");

        public void UnlockInput() { }

        public void UpdateInput(string input) { }

        public void WriteLogLine(
            string line,
            LogEventLevel level,
            IReadOnlyCollection<string>? highlightedValues = null
        ) { }
    }

    [Test]
    public async Task StartAsync_WhenConsolePromptInitializationFails_ShouldNotThrow()
    {
        var service = new ConsoleCommandService(
            new ThrowingConsoleUiService(),
            new MockCommandSystemService(),
            new GameEventBusService()
        );

        Assert.That(async () => await service.StartAsync(), Throws.Nothing);
        Assert.That(async () => await service.StopAsync(), Throws.Nothing);
    }
}
