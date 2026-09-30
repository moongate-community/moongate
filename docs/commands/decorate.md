# decorate

Places the world decoration: doors, signs, lights and furniture.

| Syntax | Console | In game | Minimum level | Role |
| --- | --- | --- | --- | --- |
| `decorate` | Yes | Yes | Administrator | Game |

```text
decorate
```

In game, administrators use `.decorate`. It places the
[decoration files](../templates.md#decorations) of `templates/decorations/`, file by file, as
fixed items that never decay; the next world save keeps them. Doors and gates get the
`decoration_door` template, whose [door script](../scripting.md) opens and closes them, and
adjacent doors of the same kind open together. Lights get the `decoration_light` template,
lit or unlit as in the data and protected, so only staff light or douse them with the
[light script](../scripting.md). Teleporters, spawners, mark containers, public
moongates and addons are skipped for now: they need their own logic.

Each file is reported when it is done, in game as a system message and in the server log:

```text
Decorating britannia/britain: 1180 placed, 3 already there, 12 skipped (Teleporter 8, Spawner 4).
```

The console and the in-game caller then get the totals:
`Decoration done: <placed> placed, <present> already there, <skipped> skipped in <files> files.`
An item with the same graphic already on the spot is kept, so running `decorate` again only
places what is missing. The files are read at each run: an edited file needs no restart. A
failure, such as a folder that is not a map, prints `The decoration failed. Check the server
logs.` and the reason goes to the log; the files done before it stay placed.

## See also

- [All commands](../commands.md)
- [`lock`](lock.md)
- [`key`](key.md)
