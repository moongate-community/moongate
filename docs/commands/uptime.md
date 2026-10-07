# uptime

Shows how long the server has been running and since when.

| Syntax | Console | In game | Minimum level | Role |
| --- | --- | --- | --- | --- |
| `uptime` | Yes | Yes | Regular | Every role |

```text
uptime
.uptime
```

For every player, and from the console. It takes no arguments and prints one line:

```text
Up for 2d 4h 13m, since 2026-10-03 10:19 UTC.
```

The time is counted from the start of the server process and shown in its largest units: days,
hours and minutes after the first day, `4h 13m` after the first hour, `13m 9s` before it and `9s`
in the first minute. The moment it started is in UTC.

## See also

- [All commands](../commands.md)
- [`version`](version.md)
