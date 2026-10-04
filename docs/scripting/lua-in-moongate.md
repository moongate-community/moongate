# Lua in Moongate

This page is part of [Writing Lua scripts](../scripting.md). The server runs Lua 5.2 through LuaCSharp, a Lua
written in C#, so no separate Lua installation is required. The language is that of the
[Lua 5.2 reference manual](https://www.lua.org/manual/5.2/); this page says what a script has and where it
sees a difference.

## The language

`_VERSION` is `"Lua 5.2"`, and `goto` works. What Lua 5.3 added does not compile: the integer division `//`,
the bitwise operators such as `&`, and the `\u{...}` escape in a string; `\x41` does.

## Libraries

Available libraries are base, `string`, `table`, `math`, restricted `coroutine` and
restricted `package`. There is no `io`, `os`, `debug`, `dofile`, `loadfile` or
`rawset`. Filesystem/native package search paths and script-created/resumed
coroutines are disabled. Use `require` for local modules and `wait` for scheduled
yielding. See the [library sandbox reference](../../src/Moongate.Scripting/README.md#sandbox)
for the exact removed functions.

| A script has | It does not have |
| --- | --- |
| `string`, `table`, `math` | `io`, `os`, `debug`, `bit32`, `utf8` |
| `load`, `pcall`, `xpcall`, `select`, `setmetatable` | `dofile`, `loadfile`, `loadstring`, `rawset` |
| `table.unpack`, `table.pack` | `unpack` |
| `coroutine.yield`, `coroutine.running`, `coroutine.status` | `coroutine.create`, `coroutine.wrap`, `coroutine.resume` |
| `require` | `package.path`, `package.cpath`, `package.loadlib`, `package.searchpath` |

`print` writes its values, separated by tabs, to the server log at Information level and returns nothing.
`string.rep` refuses a result longer than the cap given in
[Budgets and sandbox](runtime.md#budgets-and-sandbox).

## What differs from the manual

### Hexadecimal table keys

LuaCSharp does not read a hexadecimal number between brackets (`t[0x0A27]` or
`{ [0x0A27] = ... }` fail with "malformed number"): pass it through a function or a variable,
as `light.lua` does with `add(0x0A27, 0x0B1D, "circle225")`.
The number in parentheses compiles too: `t[(0x0A27)]`.

### Strings

A string counts UTF-16 units, where the manual's Lua counts bytes: `#"è"` and `#"€"` are 1, and
`string.len("añb")` is 3. Strings compare unit by unit, whatever the language of the server: `"é" < "z"` is
false.

### Numbers

Every number is a double, as in Lua 5.2. A whole value is written without a fraction: `10 / 2` gives `5`. Any
other value is written with every digit, where the manual's Lua rounds to 14: `0.1 + 0.2` gives
`0.30000000000000004`. A very large value is written with an exponent: `2 ^ 63` gives
`9.223372036854776E+18`. `string.format("%d", 3.5)` raises an error: `%d` takes a whole number.

## Calling host functions

The server publishes its functions in module tables, such as `npc` and `item`; the
[Lua API reference](https://moongate.sh/lua/) lists them with their parameters.

- A module table is read-only: assigning to one of its fields raises an error such as `'log' is read-only`,
  and its metatable is locked.
- An enum is a read-only global table, and `DirectionType.North` is a number. A parameter typed with an enum
  takes the number or the member's name in its exact case: `"North"`, not `"north"`.
- A parameter typed `integer` needs a whole number: `1.5` raises a `bad argument` error.
- A parameter the reference marks with `?` may be left out and takes its default. Leaving out another raises
  `bad argument #1 to 'module.function' (name is required)`, with the position and the name of the parameter.

To add functions of your own in C#, read [Writing a Lua module](../lua-modules.md).
