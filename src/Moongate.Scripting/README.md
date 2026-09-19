![Moongate](https://raw.githubusercontent.com/moongate-community/moongate/develop/images/moongate_logo.png)

# Moongate.Scripting

Embedded Lua 5.2 for Moongate: C# modules bound by attribute, scripts that run on the game loop, coroutines parked on the timer wheel, an instruction budget per resume, and editor definitions generated from the bindings.

## Installation

Requires .NET 10. Use the package version available in your configured NuGet feed.

```shell
dotnet add package Moongate.Scripting
```

## Features

- `[ScriptModule]`, `[ScriptFunction]` and `[ScriptConstant]` publish a C# class to Lua behind a read-only table.
- `IScriptEngine` loads files, calls functions, invalidates files for reload and reports metrics.
- `wait(seconds)` suspends a script and the timer wheel resumes it on the game loop.
- VM resumes have an instruction budget; blocking C# bindings and total memory are not bounded by it.
- `definitions.lua` and `.luarc.json` are written at startup for editor completion.

## Sandbox

Scripts see the base, `string`, `table`, `math`, `coroutine` and `package` libraries. `io`, `os` and `debug` are never opened, and the engine removes the rest of what reaches past the scripts directory or past the instruction budget: `dofile`, `loadfile` and `rawset`; `package.searchpath`, `package.path`, `package.cpath`, `package.loadlib` and the runtime's second `package.searchers` entry, which resolves `package.path` on the host filesystem independently of the module loader; and `coroutine.create`, `coroutine.wrap` and `coroutine.resume`, whose threads would carry neither the budget's hook nor its cancellation token. `coroutine.yield` stays, so `wait(seconds)` keeps working. `require` therefore resolves only under the scripts directory. `print` is replaced by a function that joins its arguments with tabs, as Lua does, and writes them to the server log at Information level under the script that called it, so script output never bypasses the configured sinks.

Memory is only partly bounded. `string.rep` refuses a result longer than `MaxStringLength` (16,777,216 characters by default, `max_string_length` in the server's `[scripting]` section; Lua strings here are UTF-16, so that is 32 MiB of text) with a script error, counted as a string cap hit in the metrics. Everything else allocates freely under the instruction budget: a table constructor, or a loop that doubles a string with `..`, can build far more than the budget suggests before it is stopped, and there is no cap on the total memory a state may hold.

## Example

Bind a module to a Lua state and call it. This runs without a Moongate server; inside the server the engine service does the binding and enforces the loop thread.

<!-- nuget-smoke:Program.cs -->
```csharp
using Lua;
using Lua.Standard;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Scripting.Binding;

using var state = LuaState.Create();
state.OpenBasicLibrary();
new LuaModuleBinder(NoThreadGuard.Instance).Bind(state, new GreeterModule());

var result = await state.DoStringAsync("return greeter.hello('Moongate')", "readme", default);
Console.WriteLine(result[0].Read<string>());

[ScriptModule("greeter", "Says hello.")]
public sealed class GreeterModule
{
    [ScriptFunction]
    public string Hello(string name)
    {
        return "Hello, " + name + "!";
    }
}
```

See [Writing Lua scripts](https://moongate-community.github.io/moongate/server/scripting/)
for bootstrap/module examples, timer ownership, reload and editor support.

## Dependencies and scope

This package depends on `Moongate.Core`, `Moongate.Server.Core`, `LuaCSharp` and `Serilog`. It does not start a game loop or timers; the host provides them and registers the engine with `RegisterMoongateService<IScriptEngine, LuaScriptEngineService>`.

## License and source

Licensed under AGPL-3.0-or-later. See the [source repository and license](https://github.com/moongate-community/moongate).
