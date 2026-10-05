# jail

Opens the gump of the jail: it lists the cells, takes you into one, and jails or releases a character.

| Syntax | Console | In game | Minimum level | Role |
| --- | --- | --- | --- | --- |
| `jail` | No | Yes | GameMaster | Game |

```text
.jail
```

In game only. It takes no argument.

## What happens

The gump of the jail opens at once, with the cells and who is inside. From there:

- `Target` gives you a cursor: pick a player or an NPC and the gump opens again on it. Then type
  the days and, if you want, the reason, and press the button of a free cell: the character is
  there.
- `Release`, on a cell that holds someone, ends its sentence with no fine.
- `Go`, on any cell, takes you into it on the map of the jail. Use it to visit a prisoner:
  [`.go cell 1`](go.md) leads to the place of that name on your own map, which is an empty room
  on every map but the jail's. The cells have no door: leave with `.go` or with another `Go`.

[Jail](../jail.md) tells the whole story: the sentence, the fine, the release note and the chest
of rations.

| You see | Why |
| --- | --- |
| `That is not a character.` | With the cursor of `Target` you picked an item or the ground, or the character left meanwhile. The gump keeps the character it had. |
| `That cell cannot be reached.` | `Go` could not take you there: the map of the jail is not loaded. |
| `The jail is not set up: data/jail.toml is missing.` | There is no [`jail.toml`](../data-files/jail.md). |
| `The jail gump is missing: templates/gumps/jail_sentence.xml.` | The gump file was removed. |

In the gump, `You cannot jail yourself or the staff of your rank.` answers a target that is you
or a player of your rank or above, and `Type the days as a whole number from 1 to 30.` days the
jail does not take.

## See also

- [All commands](../commands.md)
- [Jail](../jail.md)
- [`jail.toml`](../data-files/jail.md)
