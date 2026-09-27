# Races

`races.toml` lists the races a player can pick and, for each gender, the body and
the allowed hair and beard styles:

```toml
[[race]]
race = "human"
name = "Human"
skin_hues = ["0x03EA-0x0422"]
hair_hues = ["0x044E-0x047D"]

[race.male]
body = 400
hair = [0x203B, 0x203C, 0x203D, 0x2044, 0x2045, 0x2047, 0x2048, 0x2049, 0x204A]
beard = [0x203E, 0x203F, 0x2040, 0x2041, 0x204B, 0x204C, 0x204D]

[race.female]
body = 401
hair = [0x203B, 0x203C, 0x203D, 0x2044, 0x2045, 0x2046, 0x2047, 0x2049, 0x204A]
beard = []
```

| Field | Meaning |
| --- | --- |
| `race` | `human`, `elf` or `gargoyle` (`RaceType`). |
| `name` | The race name. |
| `skin_hues` | The allowed skin hues, as `HueSpec` values or ranges. Empty allows any hue. |
| `hair_hues` | The allowed hair and beard hues, in the same form. |
| `[race.male]`, `[race.female]` | One section per gender. |
| `body` | The body id of a living character of this race and gender. |
| `hair`, `beard` | The item ids of the allowed styles. No hair or beard (0) is always allowed and is not listed. |

At character creation a hue outside the allowed ones becomes the nearest allowed hue,
and a style not listed is dropped (`CharacterCreationRules`). A race the client
sends that is not loaded becomes human.

## Validation at startup

The server stops when:

- `races.toml` does not exist or has no `[[race]]` entries;
- a race is listed twice;
- a race has no `[race.male]` or `[race.female]` section;
- a `body` is below 1;
- a hair or beard style is outside 1 to 0xFFFF.

## See also

- [Data files overview](../data-files.md): file locations, loading order and shared value formats.
- [Check your changes](../data-files.md#check-your-changes): validate edited data before restarting.
