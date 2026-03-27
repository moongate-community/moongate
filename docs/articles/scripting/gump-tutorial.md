# Gump Tutorial

Practical tutorial for creating Lua gumps in Moongate v2.

## Goal

By the end of this guide, you will know how to:

- send a simple gump to a player
- handle button clicks
- use the file-based layout approach (`gump.send_layout`)
- load external text from `scripts/texts` into an `htmlgump`
- execute server commands from the callback

## Prerequisites

- active script runtime
- `moongate_data/scripts/init.lua` loaded
- `gump` module available (default runtime)
- `text` module available (default runtime)

## 1) Basic gump with the runtime builder

This approach builds the gump at runtime.

```lua
local SIMPLE_GUMP_ID = 0xB120
local BTN_HELLO = 101

local function open_simple_gump(session_id, character_id)
    local g = gump.create()
    g:background(0, 0, 320, 160, 9200)
    g:label(24, 20, 1152, "Moongate Gump Tutorial")
    g:button(BTN_HELLO, 24, 56, 4005, 4007)
    g:label(54, 58, 0, "Say hello")

    gump.send(session_id, g, character_id or 0, SIMPLE_GUMP_ID, 120, 80)
end
```

## 2) Button callback with `gump.on`

Callbacks receive `ctx` (session, character, button).

```lua
gump.on(0xB120, 101, function(ctx)
    if ctx.session_id ~= nil and ctx.session_id > 0 then
        speech.send(ctx.session_id, "Hello from gump callback.")
    end
end)
```

## 3) File-based layout (recommended)

This approach is cleaner when the gump grows.

File: `moongate_data/scripts/gumps/tutorial_menu.lua`

```lua
local tutorial_menu = {}

local GUMP_ID = 0xB221
local BTN_SPAWN_DOORS = 201

function tutorial_menu.open(session_id, character_id)
    local layout = {
        ui = {
            { type = "background", x = 0, y = 0, gump_id = 9200, width = 420, height = 180 },
            { type = "alpha_region", x = 12, y = 12, width = 396, height = 156 },
            { type = "label", x = 24, y = 20, hue = 1152, text = "World Tools" },
            { type = "button", id = BTN_SPAWN_DOORS, x = 24, y = 58, normal_id = 4005, pressed_id = 4007, onclick = "on_click" },
            { type = "label", x = 54, y = 60, hue = 0, text = "Spawn doors" }
        },
        handlers = {}
    }

    layout.handlers.on_click = function(ctx)
        local button = tonumber(ctx.button_id) or 0
        if button ~= BTN_SPAWN_DOORS then
            return
        end

        local lines = command.execute("spawn_doors", 1)
        if lines ~= nil and ctx.session_id ~= nil then
            for _, line in ipairs(lines) do
                if type(line) == "string" and line ~= "" then
                    speech.send(ctx.session_id, line)
                end
            end
        end
    end

    return gump.send_layout(session_id, layout, character_id or 0, GUMP_ID, 120, 80)
end

return tutorial_menu
```

## 4) Open the gump from a GM command

File: `moongate_data/scripts/commands/gm/tutorial_gump.lua`

```lua
local tutorial_menu = require("gumps.tutorial_menu")

command.register("tutorial_gump", function(ctx)
    if ctx.session_id == nil or ctx.session_id <= 0 then
        ctx:print_error("This command can only be used in-game.")
        return
    end

    local ok = tutorial_menu.open(ctx.session_id, ctx.character_id or 0)
    if not ok then
        ctx:print_error("Failed to open tutorial gump.")
    end
end, {
    description = "Open tutorial gump example.",
    minimum_account_type = "GameMaster"
})
```

Then in `init.lua`:

```lua
require("commands/gm/tutorial_gump")
```

In-game usage:

- `.tutorial_gump`

## 5) External text in `htmlgump`

File: `moongate_data/scripts/texts/welcome_player.txt`

```txt
# internal note
Welcome to {{ shard.name }}, {{ player.name }}.

Website: {{ shard.website_url }} # visible line
```

Usage from Lua:

```lua
local body = text.render("welcome_player.txt", {
    player = {
        name = "Tommy"
    }
}) or "Welcome."

local g = gump.create()
g:resize_pic(0, 0, 9200, 420, 240)
g:html(20, 20, 380, 180, body, true, true)
gump.send(session_id, g, character_id or 0, 0xB500, 120, 80)
```

Notes:

- files live under `moongate_data/scripts/texts/**`
- the syntax is Scriban (`{{ ... }}`)
- `shard.name` and `shard.website_url` are available by default
- `#` comments out the line or the trailing part of the line
- `\#` keeps a literal `#`

## 6) Quick troubleshooting

- Error “Failed to open ... gump”
  - verify that `ctx.session_id` is valid
  - verify that the file required by `require` exists
- Button click not handled
  - check `onclick` in the component (`"on_click"`)
  - check `layout.handlers.on_click`
  - verify the `button_id` used in the comparison
- Empty or “broken” gump
  - start with only `background + label`
  - add components one at a time

## 7) Layout helpers for headers and lists

For bigger file-based gumps, use the built-in helpers for repeated vertical rhythm instead of manually chaining `y = y + ...` everywhere.

Available helpers:

- `gumps.layout.header`
- `gumps.layout.stack`

Example:

```lua
local header = require("gumps.layout.header")
local stack = require("gumps.layout.stack")

local layout = {
    ui = {},
    handlers = {}
}

local next_y = header.add(layout.ui, {
    x = 24,
    y = 20,
    width = 320,
    title = "World Tools",
    subtitle = "Keep repeated vertical spacing intentional."
})

local cursor = stack.cursor(next_y)
local button_y = cursor:add(20, 10)

layout.ui[#layout.ui + 1] = {
    type = "button",
    id = BTN_SPAWN_DOORS,
    x = 24,
    y = button_y,
    normal_id = 4005,
    pressed_id = 4007,
    onclick = "on_click"
}
```

Use them for:

- title + subtitle blocks
- vertical lists with a repeated cadence

Do not turn them into a general-purpose layout DSL. If explicit coordinates make the gump easier to understand, keep them.

## Best Practices

- use `gump.send_layout` for complex gumps
- use `text.render(...)` for long text, welcome messages, rules, and books
- keep `ui` and `handlers` in the same module file
- use constants for `gumpId` and `buttonId`
- use `gumps.layout.header` and `gumps.layout.stack` only for repeated vertical spacing
- do not rely on “magic” fallbacks for `sender_serial`
- log important clicks during debugging
