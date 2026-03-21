# Reputation Titles

Moongate loads paperdoll reputation titles from Lua at startup, while keeping the display-name
formatter in C#.

## Files

- `moongate_data/scripts/config/reputation_titles_default.lua`
- `moongate_data/scripts/config/reputation_titles.lua` (optional override)
- `moongate_data/scripts/config/init.lua`

Bootstrap order:

1. `reputation_titles_default.lua`
2. optional `reputation_titles.lua` via `pcall(require, ...)`

If the optional override is missing or invalid, Moongate falls back to the built-in default table.

## Lua Shape

```lua
return {
    honorifics = {
        male = "Lord",
        female = "Lady"
    },
    fame_buckets = {
        {
            max_fame = 1249,
            karma_buckets = {
                { max_karma = -10000, title = "The Outcast" },
                { max_karma = 624, title = "" },
                { max_karma = 10000, title = "The Trustworthy" }
            }
        }
    }
}
```

## Rules

- `honorifics.male` and `honorifics.female` are used when `Fame >= 10000`
- `fame_buckets` must be ordered by ascending `max_fame`
- each `karma_buckets` list must be ordered by ascending `max_karma`
- `title` can be an empty string for neutral buckets

## Example Override

```lua
return {
    honorifics = {
        male = "Baron",
        female = "Baroness"
    },
    fame_buckets = {
        {
            max_fame = 10000,
            karma_buckets = {
                { max_karma = 10000, title = "The Custom" }
            }
        }
    }
}
```

With that override, a legendary male mobile with matching karma would display:

```text
The Custom Baron Marcus
```
