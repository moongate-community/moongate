# Professions

`professions.toml` lists the professions a player can pick at character creation.
The client sends the chosen id (packet 0xF8) and leaves skills and stats empty; the
character gets the stats and skills listed here. Id 0 is the "Advanced" choice, where the player picks
everything, so it is not listed; an id not listed is treated as 0.

```toml
[[profession]]
id = 1
name = "Warrior"
name_cliloc = 1061180
description_cliloc = 1061230
gump = 5577
str = 45
dex = 35
int = 10
skills = [
    { skill = "Tactics", value = 30 },
    { skill = "Healing", value = 30 },
    { skill = "Swordsmanship", value = 30 },
    { skill = "Anatomy", value = 30 },
]
```

| Field | Meaning |
| --- | --- |
| `id` | The profession id the client sends. |
| `name` | The profession name. |
| `name_cliloc`, `description_cliloc` | The ids of the localized name and description the client shows. |
| `gump` | The id of the gump image the client shows. |
| `str`, `dex`, `int` | The starting stats. |
| `skills` | The starting skills: the `SkillType` name without spaces (`"SpiritSpeak"`) and the value in whole points. |

The shipped professions give 90 stat points and 120 skill points, the totals of
`CharacterCreationRules` (`StatTotal = 90`, skill totals of 100 or 120). The loader
does not check these totals. Character creation uses a matching profession's stats
and skills; custom choices are validated separately by `CharacterCreationRules`.

## Validation at startup

The server stops when:

- `professions.toml` does not exist or has no `[[profession]]` entries;
- an id is below 1 or is used twice;
- a starting skill is not in `skills.toml`.

## See also

- [Data files overview](../data-files.md): file locations, loading order and shared value formats.
- [Check your changes](../data-files.md#check-your-changes): validate edited data before restarting.
