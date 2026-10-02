# Fame and karma titles

`data/titles.toml` defines the English title prefix derived from a mobile's
current fame and karma. The repository copy is
`moongate_root/data/titles.toml`; `mgctl` installs it in the server root's
`data/` directory when missing. The game server loads it at startup. Restart
the server after changing the file.

Each `[[titles]]` row defines inclusive minimum fame and karma scores. `title`
is the prefix for male characters and the default for female characters.
`female_title` is optional and overrides it for female characters. An empty
string explicitly means no prefix.

```toml
[[titles]]
fame = 10000
karma = 10000
title = "The Glorious Lord"
female_title = "The Glorious Lady"
```

| Field | Meaning |
| --- | --- |
| `fame` | Required integer minimum fame, inclusive. |
| `karma` | Required integer minimum karma, inclusive. |
| `title` | Required prefix string. `""` is valid for an untitled band. |
| `female_title` | Optional female prefix; absent means use `title`. `""` explicitly suppresses the prefix. |

The shipped file contains the classic 55 combinations: five fame bands
(`0`, `1250`, `2500`, `5000`, `10000`) and eleven karma bands (`-15000`,
`-9999`, `-4999`, `-2499`, `-1249`, `-624`, `625`, `1250`, `2500`, `5000`,
`10000`). The server selects the highest fame threshold at or below the
score, then the highest karma threshold at or below the score. Values below
the first threshold use the first band; values above the last use the last.
The order of rows in the file does not affect lookup.

Every fame threshold must have a row for every karma threshold. Startup fails
with the file path and reason if the file is missing, malformed, empty, has
missing fields, duplicate pairs, whitespace-only titles, or missing pairs.

C# callers resolve `IFameKarmaTitleService` and call `GetTitle(mobile)` or
`GetTitle(fame, karma, gender)`. The result is a prefix only, such as
`"The Glorious Lady"`; callers decide how to combine it with a name. The
service calculates the result from current scores on each call. It does not
change `MobileEntity.Title`, which remains the custom or template suffix.
The paperdoll shows the prefix before the name (see [packets](../packets.md)), `female_title` for women; tooltips and the single-click name do not, as in ModernUO.

See [data files](../data-files.md) for installation and validation guidance.
