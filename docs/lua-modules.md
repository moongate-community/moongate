# Writing a Lua module

For authoring Lua content with the existing modules, start with
[Writing Lua scripts](scripting.md). This guide extends the host bindings in C#.

A Lua module is a plain C# class marked with `[ScriptModule("name", "help text")]`. The engine binds every registered module once, at startup, on the game loop thread, and publishes it into the running `LuaState` as a read-only global table named after the attribute. On that table:

- every public instance method marked `[ScriptFunction]` becomes a callable field;
- every `[ScriptConstant]`-marked static member becomes a read-only value field;
- every enum a bound signature or constant mentions — or that is registered explicitly — is published as its own read-only global table mapping member names to numbers.

A module is registered from a plugin's `Register(Container container)` method, or, for a module the host itself owns, directly in `Program.cs` (`src/Moongate.Server/Program.cs` registers the built-in `log` module the same way, with `.AddScriptModule<LogModule>()`). Registration only records the type; nothing is reflected and no Lua table exists until the script engine starts and binds it.

Scripts run inside a sandbox: `io`, `os` and `debug` are never opened, `dofile`, `loadfile` and `rawset` are removed, and `print` is redirected to the server log instead of the console. `string.rep` also refuses to build a result past a configured cap. See the [package README's "Sandbox" section](../src/Moongate.Scripting/README.md#sandbox) for the full list.

## The sample module

The sample plugin's `Tone` enum, from `samples/Moongate.Sample.Plugin/Types/Tone.cs`:

```csharp
namespace Moongate.Sample.Plugin.Types;

/// <summary>How warmly the greeter speaks. Published to Lua as the <c>Tone</c> table.</summary>
public enum Tone
{
    /// <summary>"Hello, name!"</summary>
    Plain = 0,

    /// <summary>"Hello there, name!"</summary>
    Warm = 1,

    /// <summary>"Good day, name."</summary>
    Formal = 2
}
```

The module that takes it, from `samples/Moongate.Sample.Plugin/Modules/GreeterModule.cs`:

```csharp
using Moongate.Sample.Plugin.Internal;
using Moongate.Sample.Plugin.Types;
using Moongate.Scripting.Attributes.Scripts;

namespace Moongate.Sample.Plugin.Modules;

/// <summary>The <c>greeter</c> Lua module: one function, one constant, and the <see cref="Tone"/> enum it takes.</summary>
[ScriptModule("greeter", "Greets whoever asks.")]
public sealed class GreeterModule
{
    private readonly GreetingCounter _counter;

    /// <summary>The greeting used when no tone is given; visible to scripts as <c>greeter.DEFAULT_GREETING</c>.</summary>
    [ScriptConstant("DEFAULT_GREETING", "The plain greeting word.")]
    public static readonly string DefaultGreeting = "Hello";

    /// <summary>Initializes a new instance of the <see cref="GreeterModule"/> class.</summary>
    /// <param name="counter">Shared with the metric provider, which reports how often <see cref="Hello"/> ran.</param>
    public GreeterModule(GreetingCounter counter)
    {
        _counter = counter;
    }

    /// <summary>Builds a greeting for <paramref name="name"/> in the given <paramref name="tone"/>; scripts call it as <c>greeter.hello(name, tone)</c>.</summary>
    [ScriptFunction(helpText: "Returns a greeting for name, in the given tone.")]
    public string Hello(string name, Tone tone = Tone.Plain)
    {
        _counter.Increment();

        return tone switch
        {
            Tone.Warm => $"Hello there, {name}!",
            Tone.Formal => $"Good day, {name}.",
            _ => $"{DefaultGreeting}, {name}!"
        };
    }
}
```

A script that uses it:

```lua
greeting = greeter.hello('Moongate', Tone.Warm)
function report() return greeting, greeter.DEFAULT_GREETING end
```

At startup the engine writes `definitions.lua` for editor completion. The full file begins with a `---@meta` header and the built-in `wait` function, and from a running server also declares the built-in `engine` and `timer` modules alongside anything else registered. The excerpt below is what the sample module and its enum produce:

```lua
---@enum Tone
Tone = {
    Plain = 0,
    Warm = 1,
    Formal = 2,
}

---Greets whoever asks.
---@class greeter
---@field DEFAULT_GREETING string
greeter = {}
greeter.DEFAULT_GREETING = "Hello"

---Returns a greeting for name, in the given tone.
---@param name string
---@param tone? Tone|string
---@return string
function greeter.hello(name, tone) end
```

Note the parameter annotation: `tone? Tone|string` — the `?` because `tone` has a C# default, `Tone|string` because the converter accepts either the enum's number or a member's name on input. A function *returning* an enum would be annotated with the bare enum name — `@return Tone`, not `Tone|string` — because the engine always hands the caller the member's number, never its name.

## Functions

`[ScriptFunction]` only has an effect on **public instance methods**. A static or non-public method is never scanned, so the attribute goes unseen and the method is not published, exactly as if it carried no attribute at all.

The Lua name is the attribute's `name` argument when given (it must be a lower-case Lua identifier; the attribute rejects anything else), or the method name converted to snake_case otherwise: each run of uppercase letters starts a new lowercase, underscore-separated word, so `NextColour` becomes `next_colour`.

Parameters and returns are converted strictly, with no coercion between kinds, and range- and integer-checked for `int`/`long`:

| C# type | Accepts from Lua |
| --- | --- |
| `bool` | boolean |
| `int`, `long` | an integral number in range |
| `double`, `float` | any number |
| `string` | string |
| an enum | the underlying number (must be a defined member) or the member's name (case-sensitive) |
| a nullable value type (`int?`, `Tone?`, …) | the above, or Lua `nil` (becomes C# `null`) |
| `LuaValue` | anything, unconverted, including `nil` |
| `LuaTable` | table |
| `object` | number (as `double`), string, boolean, table, or `nil` (becomes C# `null`), unwrapped; anything else stays a `LuaValue` |
| `params` array | every remaining argument, converted one by one to the array's element type |

A wrong argument raises a Lua error of the form `bad argument #{n} to '{module}.{function}' ({reason})`, where the reason names the expected type and what was received, for example `Int32 expected, got string`, `nil cannot be converted to Int32`, `{number} is out of range for Int32`, or `'{name}' is not a member of Tone`. `LuaValue`, `object` and correctly typed `params` elements never fail.

Lua takes an enum member by name case-sensitively; `GreetCommand`'s own tone parsing on the console side is deliberately more lenient (see [Writing a plugin: Console commands](plugins.md#console-commands)). The two are independent choices for different callers.

A parameter is optional when the C# method gives it a default value, exactly like `Hello(string name, Tone tone = Tone.Plain)` above: if the script omits the argument the default is used; if the parameter is required and the script omits it, the error is `"bad argument #{n} to '{module}.{function}' ({parameter} is required)"`.

Return types are converted with the same table. `void` returns nothing to Lua. Tuples are not supported: a method returning a `System.ValueTuple` fails to bind at all with the same `"return type {Type} cannot be bound"` error described under [Registering](#registering), thrown before any script runs. A bound function always hands back at most one value; a script wanting multiple results defines its own Lua function around the call, as `report()` does in the sample above.

An exception thrown by the method itself becomes a Lua error a script can `pcall`: the binder unwraps the `TargetInvocationException` and raises `"'{module}.{function}' failed: {innerException.Message}"`.

Every bound function runs on the game loop thread — the binder checks a thread guard before each call — so it must not block, sleep, or wait on anything; see [Common mistakes](#common-mistakes).

## Constants and enums

`[ScriptConstant]` only has an effect on a **public static readonly field** or a **public static get-only property**. Anything else, such as an instance member, a non-public one, or a property with a setter, is a binding error: `"{Module}.{Member}: a [ScriptConstant] must be a public static readonly field or a public static get-only property."`

Supported constant types are `int`, `long`, `double`, `float`, `bool`, `string`, and enums — narrower than what a function parameter or return accepts: `LuaTable`, `LuaValue` and `object` are explicitly excluded even though the converter otherwise supports them. The mismatch error names the member and the type: `"{Module}.{Member}: constants of type {Type} are not supported; use int, long, double, bool, string or an enum."` A getter that throws is also a binding error, naming the member and keeping the original exception as the cause: `"{Module}.{Member}: the constant's getter threw {ExceptionType}: {Message}"`.

Every module table is a read-only proxy: reads pass through to the real table, but any assignment — to an existing key or a new one — raises `"'{name}' is read-only"`, and `setmetatable` on it raises too, since its `__metatable` is locked. `rawset`, the one call that could otherwise bypass this, is removed from the sandbox entirely.

An enum is published as a read-only global table named after the type, keyed by member name with the member's numeric value (`Tone.Plain == 0`, and so on). This happens automatically the first time a bound function's parameter or return type, or a constant's type, is that enum — or explicitly via `container.RegisterScriptEnum<TEnum>()`, which publishes it even if nothing else mentions it (`SamplePlugin.cs` does this for `Tone`, even though `GreeterModule.Hello` already causes automatic discovery). A script may pass either the member's underlying number or its exact-case name to a parameter of that enum type; the engine always returns the number. `definitions.lua` renders each one as `---@enum Name` followed by a table literal, as shown above for `Tone`.

The published table's name is exactly the C# enum's own type name, with no qualifier: a script writes `Tone.Warm`.

## Registering

The sample plugin registers the module and its enum with these two lines from `samples/Moongate.Sample.Plugin/SamplePlugin.cs`:

```csharp
container.AddScriptModule<GreeterModule>();
container.RegisterScriptEnum<Tone>();
```

`AddScriptModule<TModule>()` does two things: it registers `TModule` in the container as a `Reuse.Singleton`, and it records the type in the `IScriptModuleRegistry` the engine reads at startup. Neither action touches Lua — the engine resolves each registered type from the container and binds it only when it starts. Because the module is a container singleton, any other class that takes it as a constructor dependency resolves the very same instance: the sample's `GreetCommand(GreeterModule greeter)` is handed the identical `GreeterModule` the script engine binds, so the console command and Lua scripts share its state. A module's own constructor dependencies — here, `GreetingCounter` — must already be registered in the container by the time the engine starts, since resolution happens then, not at `AddScriptModule` time; the sample registers it first with `container.RegisterInstance(new GreetingCounter())`.

`RegisterScriptEnum<TEnum>()` only adds the registry entry; there is no DI singleton to create, since an enum is reflected, not resolved.

Binding runs once, at startup, and any failure is reported through the exceptions already listed above:

- a duplicate Lua name between two functions, or between a function and a constant, in the same module: `"{Module}.{Member}: Lua name '{name}' is already used in module '{module}'."`
- a function parameter or return type the converter cannot bind: `"{Module}.{Method}: parameter '{parameter}' of type {Type} cannot be bound."` / `"{Module}.{Method}: return type {Type} cannot be bound."`
- a `[ScriptConstant]` that is not a public static readonly field or a public static get-only property, or is of an unsupported type (see [Constants and enums](#constants-and-enums) for the exact messages).

See [Writing a plugin: registering Lua modules](plugins.md#what-register-may-do) for where `Register` fits in the plugin lifecycle.

## Testing without a server

The minimal path needs no server: open the libraries a module needs, bind it with
`LuaModuleBinder` and `NoThreadGuard.Instance` (a guard that never rejects a caller,
for hosts without a game loop), then run a chunk directly. The
[package README's example](../src/Moongate.Scripting/README.md#example) shows exactly
that with a one-function greeter module.

For the full range of conversions, error cases, and edge cases (`params`,
enum-by-number-or-name, out-of-range integers, the read-only proxy, duplicate names),
see the binder tests under `tests/Moongate.Tests/Scripting/Binding/`, which bind the
fixture modules in `tests/Moongate.Tests/TestSupport/Scripting/` the same way.

To test a module through the full stack (plugin loading, DI, the game loop, and the
generated `definitions.lua`), `tests/Moongate.Tests/Integration/Plugins/SamplePluginTests.cs`
boots a real `MoongateServerBootstrap` against a temporary plugin and scripts
directory, then calls into Lua on the loop thread, runs the console command that
shares the module, and reads the metric it recorded.

## Common mistakes

- **Returning `Task` or `ValueTask`.** Module methods run synchronously inside the bound Lua call; an async return type is not a supported type, so it fails to bind at startup with `"{Module}.{Method}: return type {Type} cannot be bound."`
- **Taking a class or interface parameter.** Only the types in the [Functions](#functions) table convert; anything else — a custom class, an interface, a collection — fails to bind with `"{Module}.{Method}: parameter '{parameter}' of type {Type} cannot be bound."`
- **Expecting `__tostring` or metatables on the module table.** The binder locks the table's metatable (`__metatable = "locked"`); `setmetatable(mymodule, {})` raises a Lua error, and no `__tostring` hook is installed.
- **Expecting the table to be writable.** Every assignment — even to a name that already exists — raises `"'{name}' is read-only"`, and `rawset`, the usual way around a metatable, is removed from the sandbox.
- **Calling into the engine from another thread.** Every bound function checks a thread guard first; calling one off the game loop thread raises `"{member} must be called on the game loop thread. Post a work item to the loop instead."`
