using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Modules;
using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Types;
using MoonSharp.Interpreter;

namespace Moongate.Tests.Server.Modules;

public class CommandModuleTests
{
    private sealed class CommandModuleTestCommandSystemService : ICommandSystemService
    {
        public string? LastCommandName { get; private set; }

        public string? LastDescription { get; private set; }

        public CommandSourceType LastSource { get; private set; }

        public AccountType LastMinimumAccountType { get; private set; }

        public Func<CommandSystemContext, Task>? LastHandler { get; private set; }
        public string? LastExecuteCommandText { get; private set; }
        public CommandSourceType LastExecuteSource { get; private set; }
        public IReadOnlyList<string> ExecuteOutput { get; set; } = [];

        public Task ExecuteCommandAsync(
            string commandWithArgs,
            CommandSourceType source = CommandSourceType.Console,
            GameSession? session = null,
            CancellationToken cancellationToken = default
        )
            => Task.CompletedTask;

        public Task<IReadOnlyList<string>> ExecuteCommandWithOutputAsync(
            string commandWithArgs,
            CommandSourceType source = CommandSourceType.Console,
            GameSession? session = null,
            CancellationToken cancellationToken = default
        )
        {
            _ = session;
            _ = cancellationToken;
            LastExecuteCommandText = commandWithArgs;
            LastExecuteSource = source;

            return Task.FromResult(ExecuteOutput);
        }

        public IReadOnlyList<string> GetAutocompleteSuggestions(string commandWithArgs)
            => [];

        public void RegisterCommand(
            string commandName,
            Func<CommandSystemContext, Task> handler,
            string description = "",
            CommandSourceType source = CommandSourceType.Console,
            AccountType minimumAccountType = AccountType.Administrator,
            Func<CommandAutocompleteContext, IReadOnlyList<string>>? autocompleteProvider = null
        )
        {
            LastCommandName = commandName;
            LastHandler = handler;
            LastDescription = description;
            LastSource = source;
            LastMinimumAccountType = minimumAccountType;
        }

        public Task StartAsync()
            => Task.CompletedTask;

        public Task StopAsync()
            => Task.CompletedTask;
    }

    [Test]
    public void Execute_ShouldForwardToCommandSystem_AndReturnOutputLines()
    {
        var commandSystemService = new CommandModuleTestCommandSystemService
        {
            ExecuteOutput = ["line one", "line two"]
        };
        var module = new CommandModule(commandSystemService);

        var result = module.Execute("help");

        Assert.Multiple(
            () =>
            {
                Assert.That(commandSystemService.LastExecuteCommandText, Is.EqualTo("help"));
                Assert.That(commandSystemService.LastExecuteSource, Is.EqualTo(CommandSourceType.Console));
                Assert.That(result.Length, Is.EqualTo(2));
                Assert.That(result.Get(1).CastToString(), Is.EqualTo("line one"));
                Assert.That(result.Get(2).CastToString(), Is.EqualTo("line two"));
            }
        );
    }

    [Test]
    public void Execute_ShouldThrow_WhenCommandTextIsEmpty()
    {
        var commandSystemService = new CommandModuleTestCommandSystemService();
        var module = new CommandModule(commandSystemService);

        Assert.That(
            () => module.Execute(string.Empty),
            Throws.TypeOf<ArgumentException>()
        );
    }

    [Test]
    public void Execute_ShouldThrow_WhenSourceIsInvalid()
    {
        var commandSystemService = new CommandModuleTestCommandSystemService();
        var module = new CommandModule(commandSystemService);

        Assert.That(
            () => module.Execute("help", 0),
            Throws.TypeOf<ArgumentOutOfRangeException>()
        );
    }

    [Test]
    public async Task Register_Handler_ShouldInvokeLuaCallback_WithLuaCommandContext()
    {
        var commandSystemService = new CommandModuleTestCommandSystemService();
        var module = new CommandModule(commandSystemService);
        var script = new Script();
        var closure = script.DoString(
                                """
                                return function(ctx)
                                    captured_command = ctx.command_text
                                    captured_is_in_game = ctx.is_in_game
                                    captured_session_id = ctx.session_id
                                    captured_arg_count = #ctx.arguments
                                end
                                """
                            )
                            .Function;

        module.Register("lua_cmd", closure);

        var context = new CommandSystemContext(
            "lua_cmd one two",
            ["one", "two"],
            CommandSourceType.InGame,
            42,
            (_, _) => { }
        );

        await commandSystemService.LastHandler!(context);

        Assert.Multiple(
            () =>
            {
                Assert.That(script.Globals.Get("captured_command").CastToString(), Is.EqualTo("lua_cmd one two"));
                Assert.That(script.Globals.Get("captured_is_in_game").CastToBool(), Is.True);
                Assert.That(script.Globals.Get("captured_session_id").CastToNumber(), Is.EqualTo(42));
                Assert.That(script.Globals.Get("captured_arg_count").CastToNumber(), Is.EqualTo(2));
            }
        );
    }

    [Test]
    public void Register_Handler_ShouldWrapLuaRuntimeErrors_WithCommandContext()
    {
        var commandSystemService = new CommandModuleTestCommandSystemService();
        var module = new CommandModule(commandSystemService);
        var script = new Script();
        var closure = script.DoString("return function(_) error('boom') end").Function;

        module.Register("lua_cmd", closure);

        var context = new CommandSystemContext(
            "lua_cmd",
            [],
            CommandSourceType.InGame,
            7,
            (_, _) => { }
        );

        Assert.That(
            async () => await commandSystemService.LastHandler!(context),
            Throws.TypeOf<InvalidOperationException>()
                  .With
                  .Message
                  .Contains("Lua command handler failed: lua_cmd")
        );
    }

    [Test]
    public void Register_ShouldIgnoreCamelCaseMinimumAccountType_AndUseDefault()
    {
        var commandSystemService = new CommandModuleTestCommandSystemService();
        var module = new CommandModule(commandSystemService);
        var script = new Script();
        var closure = script.DoString("return function(_) end").Function;
        var options = new Table(script)
        {
            ["source"] = (int)CommandSourceType.InGame,
            ["minimumAccountType"] = (int)AccountType.GameMaster
        };

        module.Register("lua_cmd", closure, options);

        Assert.That(commandSystemService.LastMinimumAccountType, Is.EqualTo(AccountType.Regular));
    }

    [Test]
    public void Register_ShouldRegisterCommand_WithProvidedOptions()
    {
        var commandSystemService = new CommandModuleTestCommandSystemService();
        var module = new CommandModule(commandSystemService);
        var script = new Script();
        var closure = script.DoString("return function(_) end").Function;
        var options = new Table(script)
        {
            ["description"] = "lua registered command",
            ["source"] = (int)(CommandSourceType.Console | CommandSourceType.InGame),
            ["minimum_account_type"] = (int)AccountType.GameMaster
        };

        module.Register("lua_cmd", closure, options);

        Assert.Multiple(
            () =>
            {
                Assert.That(commandSystemService.LastCommandName, Is.EqualTo("lua_cmd"));
                Assert.That(commandSystemService.LastDescription, Is.EqualTo("lua registered command"));
                Assert.That(
                    commandSystemService.LastSource,
                    Is.EqualTo(CommandSourceType.Console | CommandSourceType.InGame)
                );
                Assert.That(commandSystemService.LastMinimumAccountType, Is.EqualTo(AccountType.GameMaster));
                Assert.That(commandSystemService.LastHandler, Is.Not.Null);
            }
        );
    }

    [Test]
    public void Register_ShouldThrow_WhenHandlerIsNull()
    {
        var commandSystemService = new CommandModuleTestCommandSystemService();
        var module = new CommandModule(commandSystemService);

        Assert.That(
            () => module.Register("test_cmd", null!),
            Throws.TypeOf<ArgumentNullException>()
        );
    }

    [Test]
    public void Register_ShouldThrow_WhenNameIsEmpty()
    {
        var commandSystemService = new CommandModuleTestCommandSystemService();
        var module = new CommandModule(commandSystemService);
        var closure = new Script().DoString("return function(_) end").Function;

        Assert.That(
            () => module.Register(string.Empty, closure),
            Throws.TypeOf<ArgumentException>()
        );
    }
}
