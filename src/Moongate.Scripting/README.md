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
- Every resume is bounded by an instruction count, so a runaway script cannot stall the loop.
- `definitions.lua` and `.luarc.json` are written at startup for editor completion.

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

## Dependencies and scope

This package depends on `Moongate.Core`, `Moongate.Server.Core`, `LuaCSharp` and `Serilog`. It does not start a game loop or timers; the host provides them and registers the engine with `RegisterMoongateService<IScriptEngine, LuaScriptEngineService>`.

## License and source

Licensed under AGPL-3.0-or-later. See the [source repository and license](https://github.com/moongate-community/moongate).
