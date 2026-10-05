# Skills

`skills.toml` lists the 58 skills. Each `id` names a `SkillType`, whose value is the
number the client uses (0 to 57), so the entries stay in that order, from `alchemy` to
`throwing`:

```toml
[[skill]]
id = "alchemy"
name = "Alchemy"
title = "Alchemist"
profession_name = "Alchemy"
primary_stat = "int"
secondary_stat = "dex"
str_scale = 0.0
dex_scale = 5.0
int_scale = 5.0
str_gain = 0.0
dex_gain = 0.5
int_gain = 0.5
gain_factor = 1.0
```

| Field | Meaning |
| --- | --- |
| `id` | The skill, by `SkillType` name; its value is the client's skill id. |
| `name` | The skill name. |
| `title` | The title of a character whose best skill is this one. |
| `profession_name` | The name the profession files use for the skill. |
| `primary_stat`, `secondary_stat` | The stats the skill depends on: `str`, `dex` or `int`. |
| `str_scale`, `dex_scale`, `int_scale` | The chance, in percent, that a skill gain also raises that stat. |
| `str_gain`, `dex_gain`, `int_gain` | How much a skill gain favours that stat when a stat rises. |
| `gain_factor` | How fast the skill rises; 1.0 is normal. |
| `delay` | Optional. The seconds a character waits before another skill after using this one, from 0 to 3600; `hiding` has `delay = 10.0`. Without it, and when the script of the skill returns no number, one second. |

The [skills](../skills.md) read `gain_factor` when a skill is gained and `delay` when it is used; the
stat fields are not read yet.

## Validation at startup

The server stops when:

- `skills.toml` does not exist or has no `[[skill]]` entries;
- the ids are not in `SkillType` order without gaps: entry 0 must be `alchemy`
  (value 0), entry 1 `anatomy` (value 1), and so on;
- an id is a number or not a `SkillType` name;
- a `delay` is below 0 or above 3600.

## See also

- [Data files overview](../data-files.md): file locations, loading order and shared value formats.
- [Check your changes](../data-files.md#check-your-changes): validate edited data before restarting.
