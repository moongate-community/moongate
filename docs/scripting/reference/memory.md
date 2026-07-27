# memory

`memory` is durable key/value storage for the current NPC brain. Every function
acts on the mobile of the running tick — there is no serial argument — and
values persist across server restarts and brain changes. Calling any `memory`
function outside a brain hook raises a Lua error.

Use it for facts the NPC should remember long-term. For per-tick scratch that
does not need to survive a restart, use the `state` table instead (see the
[NPC brains guide](../guides/npc-brains.md#state-vs-memory)).

Values are scalars: `string`, `number`, or `boolean`. Bounds: at most 128 keys
per NPC, keys up to 64 characters, string values up to 256 characters; a `set`
past a bound returns `false`.

## memory.set

```lua
memory.set(key, value) -> boolean
```

Stores `value` (a string, number, or boolean) under `key`. Returns `false` when
the value is not a supported scalar, a bound is exceeded, or the NPC no longer
exists.

## memory.get

```lua
memory.get(key) -> value | nil
```

Returns the stored value with its original type, or `nil` when the key is unset.

## memory.delete

```lua
memory.delete(key) -> boolean
```

Removes `key`. Returns `true` when it existed.

## memory.all

```lua
memory.all() -> table
```

Returns a table of every stored `key = value` for the current NPC.
