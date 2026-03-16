# Lua Plugins

Moongate can load gameplay extensions from a dedicated `plugins/` directory without modifying the core `moongate_data/scripts` tree.

This first version is intentionally simple:

- plugins are Lua-only
- metadata lives in `plugin.lua`
- the entry script is `init.lua`
- plugins load after the core scripts
- `.reload_scripts` reloads core scripts and plugins together
- no single-plugin unload or dependency graph exists yet

## Plugin Layout

Each plugin lives under:

```text
plugins/<plugin-id>/
  plugin.lua
  init.lua
```

Optional folders:

- `gumps/`
- `commands/`
- `ai/`
- `items/`

Example:

```text
plugins/helpplus/
  plugin.lua
  init.lua
  gumps/
    helpplus.lua
  commands/
    helpplus.lua
```

## Manifest

`plugin.lua` must return a Lua table:

```lua
return {
  id = "helpplus",
  name = "Help Plus",
  version = "0.1.0",
  entry = "init.lua",
}
```

Required fields:

- `id`
- `entry`

If the manifest is invalid, or if another plugin already uses the same `id`, the plugin is skipped.

## Namespaced Requires

Plugin modules use the namespace:

```lua
plugin.<plugin-id>.*
```

Examples:

```lua
local help_gump = require("plugin.helpplus.gumps.helpplus")
local help_command = require("plugin.helpplus.commands.helpplus")
```

This avoids collisions with core modules such as `gumps.*`, `items.*`, and `ai.*`.

## Entry Script

`init.lua` is the plugin entry point. It should require the modules it needs and register behavior using the existing script APIs.

Example:

```lua
local help_gump = require("plugin.helpplus.gumps.helpplus")

command.register("helpplus", function(ctx)
    if ctx.session_id ~= nil and ctx.character_id ~= nil then
        help_gump.open(ctx.session_id, ctx.character_id)
    end
end, {
    description = "Open the Help Plus gump.",
    minimum_account_type = "GameMaster"
})
```

## Supported Extension Types

Plugins can officially register:

- GM commands
- custom gumps and gump callbacks
- event hook functions
- NPC behaviors / brains
- item scripts

They use the same Lua modules and game-facing APIs as core scripts.

## Current Limitations

Not supported in v1:

- single-plugin unload
- per-plugin dependency ordering
- sandboxing or capability restrictions
- remote installation or version negotiation

## Operational Note

The core script file watcher still focuses on the scripting bootstrap flow. For now, treat plugins as part of the normal script reload cycle instead of expecting hot unload/reload per plugin.
