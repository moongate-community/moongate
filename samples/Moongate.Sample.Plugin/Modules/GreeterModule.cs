using Moongate.Sample.Plugin.Internal;
using Moongate.Sample.Plugin.Types;
using Moongate.Scripting.Attributes.Scripts;

namespace Moongate.Sample.Plugin.Modules;

/// <summary>The <c>greeter</c> Lua module: one function, one constant, and the <see cref="Tone" /> enum it takes.</summary>
[ScriptModule("greeter", "Greets whoever asks.")]
public sealed class GreeterModule
{
    private readonly GreetingCounter _counter;

    /// <summary>The greeting used when no tone is given; visible to scripts as <c>greeter.DEFAULT_GREETING</c>.</summary>
    [ScriptConstant("DEFAULT_GREETING", "The plain greeting word.")]
    public static readonly string DefaultGreeting = "Hello";

    /// <summary>Initializes a new instance of the <see cref="GreeterModule" /> class.</summary>
    /// <param name="counter">Shared with the metric provider, which reports how often <see cref="Hello" /> ran.</param>
    public GreeterModule(GreetingCounter counter)
    {
        _counter = counter;
    }

    /// <summary>
    /// Builds a greeting for <paramref name="name" /> in the given <paramref name="tone" />; scripts call it as
    /// <c>greeter.hello(name, tone)</c>.
    /// </summary>
    [ScriptFunction(helpText: "Returns a greeting for name, in the given tone.")]
    public string Hello(string name, Tone tone = Tone.Plain)
    {
        _counter.Increment();

        return tone switch
        {
            Tone.Warm   => $"Hello there, {name}!",
            Tone.Formal => $"Good day, {name}.",
            _           => $"{DefaultGreeting}, {name}!"
        };
    }
}
