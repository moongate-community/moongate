# script

Reloads one Lua script or prints the script engine counters.

| Syntax | Console | In game | Minimum level | Role |
| --- | --- | --- | --- | --- |
| `script reload <file>` / `script metrics` | Yes | No | — | Game |

```text
script reload ai/guard.lua
script metrics
```

`script reload` reloads one file relative to the configured `scripts/` directory.
It runs the reload on the game loop and reports an error if the script fails to
load. `script metrics` prints the Lua engine's current counters.

## See also

- [All commands](../commands.md)
