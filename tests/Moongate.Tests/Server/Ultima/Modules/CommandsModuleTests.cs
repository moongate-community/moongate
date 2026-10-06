using Lua;
using Lua.Standard;
using Moongate.Scripting.Binding;
using Moongate.Scripting.Internal;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Tests.TestSupport.Commands;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Ultima.Speech;

namespace Moongate.Tests.Server.Ultima.Modules;

public sealed class CommandsModuleTests : IAsyncLifetime
{
    private readonly RecordingCommandSystemService _commands = new();

    private BroadcastFixture _fixture = null!;
    private GameSession _session = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _session = await _fixture.AddAsync(2);
    }

    // As the server console: every power, and no player behind it.
    [Fact]
    public void Execute_RunsTheCommandAsTheConsole_WithItsArgumentsInOneLine()
    {
        var result = Run(
            "return commands.execute('season', 'winter'), commands.execute('save'), commands.execute('time', 12, true, 1.5)"
        );

        Assert.All(result, value => Assert.True(value.Read<bool>()));
        Assert.Equal(
            [
                ("season winter", CommandSourceType.Console, null), ("save", CommandSourceType.Console, null),
                ("time 12 true 1.5", CommandSourceType.Console, (GameSession?)null)
            ],
            _commands.Executed
        );
    }

    // As that player wrote it: its session, so its account level decides, and it reads the answer.
    [Fact]
    public void ExecuteAs_RunsTheCommandAsThePlayer_AndThePlayerReadsWhatItAnswers()
    {
        _commands.Output.Add(new("Going to Britain.", CommandOutputLevel.Information));
        _commands.Output.Add(new("Careful.", CommandOutputLevel.Warning));

        var result = Run("return commands.execute_as(2, 'go', 'britain')");

        Assert.True(result[0].Read<bool>());
        Assert.Equal(("go britain", CommandSourceType.InGame, _session), Assert.Single(_commands.Executed));
        Assert.Equal(
            ["Going to Britain.", "Careful."],
            _fixture.Sender.Sent.OfType<UnicodeSpeechMessagePacket>().Select(message => message.Text)
        );
    }

    [Theory]
    [InlineData("commands.execute('')")]
    [InlineData("commands.execute('   ')")]
    // One line a command: nothing that would start a second one.
    [InlineData("commands.execute('save\\nshutdown')")]
    [InlineData("commands.execute('time', 'now\\rshutdown')")]
    // A table or a function is no argument of a command.
    [InlineData("commands.execute('time', {})")]
    // Nobody to run it as.
    [InlineData("commands.execute_as(999, 'time')")]
    [InlineData("commands.execute_as(-1, 'time')")]
    [InlineData("commands.execute_as(2, '')")]
    public void WhatIsNoCommand_OrHasNobodyToRunItAs_RunsNothing(string call)
    {
        var result = Run("return " + call);

        Assert.False(result[0].Read<bool>());
        Assert.Empty(_commands.Executed);
    }

    // A command that fails is the server's to log, never the script's to catch.
    [Fact]
    public void ACommandThatThrows_DoesNotReachTheScript()
    {
        _commands.Throws = new InvalidOperationException("boom");

        var result = Run("return commands.execute('save'), commands.execute_as(2, 'time')");

        Assert.All(result, value => Assert.True(value.Read<bool>()));
        Assert.Equal(2, _commands.Executed.Count);
    }

    private LuaValue[] Run(string chunk)
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        var binder = new LuaModuleBinder(NoThreadGuard.Instance);
        binder.Bind(state, new CommandsModule(_commands, _fixture.Sessions, _fixture.Sender));

        return SyncValueTask.Run(state.DoStringAsync(chunk, "t"));
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }
}
