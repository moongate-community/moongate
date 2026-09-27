# Banned names

`banned_names.toml` holds the words a player character name may not use. Matching
ignores case.

```toml
starts_with = [
    "admin",
    "counselor",
    "gm",
]

words = [
    "adept",
    "apprentice",
]
```

| Field | Meaning |
| --- | --- |
| `starts_with` | Words a name may not start with: `gm` also bans `GMaria`. |
| `words` | Words a name may not contain as a whole word: `mage` bans `Aria the Mage` but not `Magenta`. |

The loader trims every word and returns one `BannedNamesContent`. At character
creation a banned or malformed name becomes `Generic Player`
(`CharacterCreationRules.ValidateName`). Names are not unique.

## Validation at startup

The server stops when:

- `banned_names.toml` does not exist;
- a word is empty after trimming, which would ban every name.

## See also

- [Data files overview](../data-files.md): file locations, loading order and shared value formats.
- [Check your changes](../data-files.md#check-your-changes): validate edited data before restarting.
