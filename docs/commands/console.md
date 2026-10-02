# console

Locks the console input again, as it is at startup.

| Syntax | Console | In game | Minimum level | Role |
| --- | --- | --- | --- | --- |
| `console lock` | Yes | No | — | Every role |

```text
console lock
```

The console starts locked: keys typed by accident reach no command until `*` is pressed. Once
unlocked it stays open; `console lock` locks it again and says which key unlocks it:

```text
Console locked. Press '*' to unlock.
```

While it is locked, the first key pressed that is not `*` logs a warning, once until the next
unlock:

```text
Console input is locked. Press '*' to unlock.
```

Anything other than `console lock` shows the usage.

## See also

- [All commands](../commands.md)
