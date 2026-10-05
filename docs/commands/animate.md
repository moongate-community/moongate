# animate

Makes the character or NPC you target play an action of its body.

| Syntax | Console | In game | Minimum level | Role |
| --- | --- | --- | --- | --- |
| `animate <action>`, then target a mobile | No | Yes | GameMaster | Game |

```text
.animate 21
```

In game only. Everyone who sees the mobile sees it play the action once, and you read `an orc plays
action 21.` The action is a number from 0 to 65535 of the mobile's body: a human, a monster and an
animal do not share them. For a human body 21 falls dead forward and 22 backward, 32 bows, 33
salutes, 34 eats; the names are those of `HumanAnimationType`, `MonsterAnimationType` and
`AnimalAnimationType` in [the Lua reference](../scripting.md). Nothing else changes: a mobile that
plays its death stays alive.

- An item, or nothing a mobile: `That is not a character or an NPC.`

## See also

- [All commands](../commands.md)
- [`kill`](kill.md)
