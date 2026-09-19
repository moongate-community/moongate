using Moongate.Sample.Plugin.Modules;
using Moongate.Sample.Plugin.Types;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Interfaces.Commands;

namespace Moongate.Sample.Plugin.Commands;

/// <summary>"greet &lt;name&gt; [plain|warm|formal]": prints a greeting on the console through the same module scripts use.</summary>
public sealed class GreetCommand : ICommandExecutor
{
    private const string Usage = "Usage: greet <name> [plain|warm|formal]";

    private readonly GreeterModule _greeter;

    /// <summary>Initializes a new instance of the <see cref="GreetCommand"/> class.</summary>
    /// <param name="greeter">The module singleton the script engine binds; its method is plain C#, safe to call from the console thread.</param>
    public GreetCommand(GreeterModule greeter)
    {
        _greeter = greeter;
    }

    /// <inheritdoc />
    public Task ExecuteAsync(CommandContext context)
    {
        if (context.Arguments.Length is 0 or > 2)
        {
            context.PrintError(Usage);

            return Task.CompletedTask;
        }

        var tone = Tone.Plain;

        if (context.Arguments.Length == 2 && !Enum.TryParse(context.Arguments[1], ignoreCase: true, out tone))
        {
            context.PrintError(Usage);

            return Task.CompletedTask;
        }

        context.Print(_greeter.Hello(context.Arguments[0], tone));

        return Task.CompletedTask;
    }
}
