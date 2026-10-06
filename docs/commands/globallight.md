# globallight

Gives every player the same light, or goes back to the time of day.

| Syntax | Console | In game | Minimum level | Role |
| --- | --- | --- | --- | --- |
| `globallight [0-31]` | Yes | Yes | GameMaster | Game |

```text
globallight 26
globallight
```

In game, game masters use `.globallight 26`. With a level from 0 (brightest) to 31 (darkest),
every player in the world gets that light at once: `The global light is now 26.` Without a
level, the light follows the time of day again (see
[the light cycle](../server-configuration.md)). The override is not saved: a restart goes back to
the time of day.

## See also

- [All commands](../commands.md)
- [`weather`](weather.md)
- [`gmtools`](gmtools.md): the same levels as buttons
