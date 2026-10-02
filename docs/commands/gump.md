# gump

Opens a gump of `templates/gumps` on you, to try it.

| Syntax | Console | In game | Minimum level | Role |
| --- | --- | --- | --- | --- |
| `gump <id> [name=value ...]` | No | Yes | GameMaster | Game |

```text
.gump tutorial_name
.gump tutorial_greeting name=Aria
```

In game only. It opens the gump as `gump.open` would from a script: its slots are filled, and its
script gets the answers. The `name=value` pairs fill its `${name}` placeholders: names are lower
cased, as placeholders are, and a value may contain `=` but not spaces. An unknown id prints
`No gump <id> in templates/gumps.`; a gump that cannot open, such as one whose slot function fails,
prints `Gump <id> could not open.`
See [Gumps](../gumps.md) and [Your first gump](../gump-tutorial.md).

## See also

- [All commands](../commands.md)
- [`decorate`](decorate.md)
