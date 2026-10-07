# jail

Opens the gump of the jail: it lists the cells, takes you into one, and jails or releases a character; jail <name> opens it on the player of that name, online or not.

| Syntax | Console | In game | Minimum level | Role |
| --- | --- | --- | --- | --- |
| `jail [name]` | No | Yes | GameMaster | Game |

```text
.jail
.jail Pippo
.jail Lord Pippo
```

In game only. The name is the whole name of a player character, typed in any case; a name of
several words needs no quotes.

## What happens

The gump of the jail opens at once, with the cells and who is inside. From there:

- `Target` gives you a cursor: pick a player or an NPC and the gump opens again on it. Then type
  the days and, if you want, the reason, and press the button of a free cell: the character is
  there.
- `Release`, on a cell that holds someone, ends its sentence with no fine.

With a name, the gump opens on the player who has it, in the world or not:

- A player who is online is the target, as if you had picked it with the cursor.
- A player who is offline is the target too, shown as `Pippo (offline)`. Press the button of a free
  cell and you read `Pippo will be in cell 3 for 3 days from its next login.`: the cell is kept
  for it, and its days start when it logs in. See
  [Jail a player who is offline](../jail.md#jail-a-player-who-is-offline).
- Several players of that name are listed with their account and whether they are online. Press
  the button of the one you mean and the gump opens on it, with the cells.

- `Go`, on any cell, takes you into it on the map of the jail. Use it to visit a prisoner:
  [`.go cell 1`](go.md) leads to the place of that name on your own map, which is an empty room
  on every map but the jail's. The cells have no door: leave with `.go` or with another `Go`.

[Jail](../jail.md) tells the whole story: the sentence, the fine, the release note and the chest
of rations.

| You see | Why |
| --- | --- |
| `No character is named Pippo.` | No player character has that name. NPCs are not looked for, nor characters that wait to be deleted. |
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
