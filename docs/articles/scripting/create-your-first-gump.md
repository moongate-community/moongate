# Create Your First Gump

This guide upgrades the item script from the previous tutorial so it opens a simple UI instead of only sending a text message.

By the end, you will:

- register one `gump.on(...)` callback
- send a gump from an item script
- react to a button click

## Before You Start

Finish this guide first:

- [Create Your First Item Script](create-your-first-item-script.md)

You also need:

- your shard root is `~/moongate`
- your server is configured to run against `~/moongate`
- you can log in with an account that can run `.spawn_item`

## Step 1: Replace The Item Script With A Gump Version

Open:

```text
~/moongate/scripts/items/tutorial_brick.lua
```

Replace the file with this content:

```lua
local TUTORIAL_BRICK_GUMP_ID = 0xB320
local TUTORIAL_BRICK_HELLO_BUTTON = 1

gump.on(TUTORIAL_BRICK_GUMP_ID, TUTORIAL_BRICK_HELLO_BUTTON, function(ctx)
    if ctx == nil or ctx.session_id == nil then
        return
    end

    speech.send(ctx.session_id, "You clicked the tutorial gump button.")
end)

tutorial_brick = {
    on_double_click = function(ctx)
        if ctx == nil or ctx.session_id == nil then
            return
        end

        local sender = ctx.mobile_id or 0

        local g = gump.create()
        g:resize_pic(0, 0, 9200, 300, 160)
        g:no_move()
        g:text(24, 20, 1152, "Tutorial Brick")
        g:text(24, 54, 0, "This is my first Moongate gump.")
        g:button(24, 100, 4005, 4007, TUTORIAL_BRICK_HELLO_BUTTON)
        g:text(58, 102, 0, "Say hello")

        gump.send(ctx.session_id, g, sender, TUTORIAL_BRICK_GUMP_ID, 120, 80)
    end,
}
```

What changed:

- `gump.on(...)` registers the button callback
- `on_double_click` now builds and sends a gump
- the button click sends a text response back to the player

## Step 2: Restart The Server

Restart the server so the updated item script is loaded.

## Step 3: Spawn The Item

In game, run:

```text
.spawn_item tutorial_brick
```

Click a nearby tile to place the item.

## Step 4: Open The Gump

Double-click the item.

Expected result:

- a gump titled `Tutorial Brick` opens
- clicking `Say hello` sends `You clicked the tutorial gump button.`

## Common Mistakes

- Forgetting that `gump.on(...)` must use the same `gumpId` and `buttonId` as the gump you send
- Editing the item script but not restarting the server
- Replacing the `tutorial_brick` table name with something that no longer matches the template `scriptId`

## Optional: Layout Helpers For Vertical Rhythm

When a gump starts to grow, stop hand-counting every vertical offset. Moongate includes two small helpers for the most repetitive spacing work:

- `gumps.layout.header` for `title + subtitle`
- `gumps.layout.stack` for vertical lists and stacked blocks

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
    width = 260,
    title = "Tutorial Brick",
    subtitle = "Use helpers for repeated vertical rhythm, not for every coordinate."
})

local cursor = stack.cursor(next_y)
local first_button_y = cursor:add(20, 12)

layout.ui[#layout.ui + 1] = {
    type = "button",
    id = TUTORIAL_BRICK_HELLO_BUTTON,
    x = 24,
    y = first_button_y,
    normal_id = 4005,
    pressed_id = 4007,
    onclick = "on_click"
}
```

Use these helpers when:

- you have repeated vertical spacing
- you want the next `y` without recounting by hand

Do not use them as a full layout system. Keep explicit coordinates when they make the gump easier to read.

## Next Step

Continue with [Create Your First Lua Admin Command](create-your-first-lua-admin-command.md).
