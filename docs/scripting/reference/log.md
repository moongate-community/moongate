# log

Writes to the server log. Backed by `LoggerModule`, which forwards to Serilog.

Every function takes a **message template** and optional positional arguments.
The template uses Serilog placeholders (`{0}`, `{1}`, …) that are filled from
the extra arguments in order. None of these functions return a value.

## log.debug

```lua
log.debug(message, ...)
```

Writes a message at the **Debug** level.

**Example**

```lua
log.debug("spawned mobile {0} at ({1},{2})", serial, x, y)
```

## log.info

```lua
log.info(message, ...)
```

Writes a message at the **Information** level.

**Example**

```lua
log.info("world ready")
```

## log.warn

```lua
log.warn(message, ...)
```

Writes a message at the **Warning** level.

**Example**

```lua
log.warn("template {0} produced no item", "dagger")
```

## log.error

```lua
log.error(message, ...)
```

Writes a message at the **Error** level.

**Example**

```lua
log.error("failed to equip item {0} on mobile {1}", blade, guard)
```
