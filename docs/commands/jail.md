# jail

Sends the character you target to a jail cell for some days, or releases it.

| Syntax | Console | In game | Minimum level | Role |
| --- | --- | --- | --- | --- |
| `jail`, then target a character | No | Yes | GameMaster | Game |

```text
.jail
```

In game only. It takes no argument.

## What happens

You get a target cursor. Target a player or an NPC and the gump of the jail opens on it: type
the days, press the button of a free cell, and the character is there. A cell that holds someone
shows who and for how long, with a `Release` button. [Jail](../jail.md) tells the whole story:
the sentence, the fine, the release note and the chest of rations.

| You see | Why |
| --- | --- |
| `Target canceled.` | You pressed Escape. |
| `That is not a character.` | You targeted an item or the ground, or the character left meanwhile. |
| `The jail is not set up: data/jail.toml is missing.` | There is no [`jail.toml`](../data-files/jail.md). |
| `The jail gump is missing: templates/gumps/jail_sentence.xml.` | The gump file was removed. |

In the gump, `You cannot jail yourself or the staff of your rank.` answers a target that is you
or a player of your rank or above, and `Type the days as a whole number from 1 to 30.` days the
jail does not take.

## See also

- [All commands](../commands.md)
- [Jail](../jail.md)
- [`jail.toml`](../data-files/jail.md)
