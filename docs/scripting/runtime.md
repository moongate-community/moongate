# Reload, budgets and editor

This page is part of [Writing Lua scripts](../scripting.md).

## Reload and ownership

The console accepts:

```text
script reload init.lua
script metrics
```

Reload invalidates the named file, evicts its matching `require` cache entry,
cancels timers/coroutines owned by that file and executes it again on the loop.
It does not rebuild the whole Lua state, recursively invalidate dependencies or
roll back globals if the new file fails. Existing references to a module table
remain references to that old table. After changing a required helper, invalidate
that helper and reload the consuming script to obtain the new module value.

Ownership follows the engine's active load/coroutine context. In particular,
`require` executes a module within its caller's context; timers created there
must not be assumed to belong to the module filename. Prefer modules that return
functions/data and let `init.lua` or an explicitly loaded owner create timers.
This makes cancellation on reload predictable. Engine shutdown cancels all owned
scheduled work before disposing its Lua state.

`script metrics` shows file loads, calls, coroutine resumes/completions/errors,
active coroutines, budget aborts, string-cap hits and server events dropped
because the game loop refused them (each drop is also logged as a warning). Script errors include source
information where available, are logged, and publish `ScriptErrorEvent`. Ordinary
runtime errors are reported by the scheduler; host C# callers of `LoadFile` must
handle its exceptions. Missing files and cancellation have their own failure paths.

## Budgets and sandbox

Defaults allow 150,000 instructions per coroutine resume and 10,000,000 per
loaded top-level chunk, checked every 1,000 instructions. They bound VM instruction
execution, not wall-clock duration or native C# work. A module that blocks on I/O
can still block the loop; keep host-bound functions short and synchronous.
`string.rep` caps its result at 16,777,216 UTF-16 characters by default. Other
allocations, including tables and concatenation, do not have a global memory cap.
Treat scripts and C# plugins as trusted shard content, not as an isolation boundary
for arbitrary hostile code.

Which libraries a script has, and where the Lua of the server differs from the manual, is told in
[Lua in Moongate](lua-in-moongate.md).

## Editor support

With `write_definitions = true`, startup writes `scripts/definitions.lua` and
`scripts/.luarc.json` from registered modules, functions, constants and enums.
Open the scripts folder in an editor using the Lua language server for completion.
These files are generated: put handwritten content in separate files and register
C# modules before startup so they appear in the definitions. They provide editor
metadata, not runtime loading; do not `require("definitions")`.
