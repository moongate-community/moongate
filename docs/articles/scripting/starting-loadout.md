# Lua Starting Loadout

Moongate builds the initial character inventory from Lua through the `build_starting_loadout(context)` hook.

This replaces the older JSON startup template pipeline. Character creation rules now live under:

- `moongate_data/scripts/startup/init.lua`
- `moongate_data/scripts/startup/starting_loadout_default.lua`
- `moongate_data/scripts/startup/starting_loadout.lua`

The server still owns item creation, placement, validation, and persistence. Lua only returns the loadout definition.

## Default and Override Scripts

`startup/init.lua` loads the scripts in this order:

1. `starting_loadout_default.lua`
2. `starting_loadout.lua` via `pcall(require, ...)`

This gives you a safe default starter loadout in source control and an easy override point for local or shard-specific customization.

- `starting_loadout_default.lua`: minimal fallback shipped with Moongate
- `starting_loadout.lua`: optional custom override for your shard rules

## Hook Contract

The hook receives a single `context` table:

```lua
{
  player_name = "Lyra",
  race = "human",
  gender = "female",
  profession = "Mage"
}
```

The hook must return a table with two optional arrays:

```lua
return {
  backpack = {
    {
      template_id = "Gold",
      amount = 1000
    },
    {
      template_id = "Spellbook",
      args = {
        title = "Arcane Notes",
        author = context.player_name,
        pages = 32,
        writable = true
      }
    }
  },
  equip = {
    {
      template_id = "Shirt",
      layer = "Shirt"
    },
    {
      template_id = "BankBox",
      layer = "Bank"
    }
  }
}
```

## Fields

- `template_id`: item template id from `templates/items`
- `amount`: optional stack amount, defaults to `1`
- `args`: optional object copied into item custom properties
- `layer`: required for `equip` entries; must match `ItemLayerType`

## Item Args

Lua computes dynamic values directly. There is no placeholder resolution phase.

For example, use:

```lua
author = context.player_name
```

instead of a placeholder such as `"<playerName>"`.

The current C# bridge maps these common book aliases automatically:

- `title` -> `book_title`
- `author` -> `book_author`
- `content` -> `book_content`
- `pages` -> `book_pages`
- `writable` -> `book_writable`

Other scalar values are written as item custom properties with the same key.

## Notes

- Backpack creation remains server-side.
- Equipment entries without `layer` are rejected.
- If the hook is missing, Moongate falls back to an empty scripted loadout.
- Keep loadout logic content-oriented. Do not perform world mutations from Lua for character creation.
