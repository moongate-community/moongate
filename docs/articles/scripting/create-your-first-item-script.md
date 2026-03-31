# Create Your First Item Script

This guide is the shortest path from a static item template to an item that does something when a player uses it.

By the end, you will:

- point an item template at a Lua `scriptId`
- create one Lua file under `~/moongate/scripts/items/`
- register it in `~/moongate/scripts/items/init.lua`
- spawn the item and trigger the script in game

## Before You Start

Finish this guide first:

- [Create Your First Item Template](create-your-first-item-template.md)

You also need:

- your shard root is `~/moongate`
- `moongate-template` is installed
- your server is configured to run against `~/moongate`
- you can log in with an account that can run `.spawn_item`

## Step 1: Point The Item Template At A Script

Open:

```text
~/moongate/templates/items/tutorial/first_item.json
```

Replace the file with this complete version:

```json
[
  {
    "type": "item",
    "id": "tutorial_brick",
    "name": "Tutorial Brick",
    "category": "tutorial",
    "description": "My first scripted Moongate item template.",
    "tags": ["tutorial", "test"],
    "itemId": "0x1F9E",
    "hue": "0",
    "goldValue": "0",
    "weight": 1,
    "scriptId": "tutorial_brick",
    "isMovable": true
  }
]
```

Important part:

- `scriptId` is now `tutorial_brick`
- that tells the item script dispatcher to look for a Lua table named `tutorial_brick`

## Step 2: Create The Lua Script File

Create:

```text
~/moongate/scripts/items/tutorial_brick.lua
```

Paste this content:

```lua
tutorial_brick = {
    on_double_click = function(ctx)
        if ctx == nil or ctx.session_id == nil then
            return
        end

        speech.send(ctx.session_id, "Tutorial Brick script executed.")
    end,
}
```

What this does:

- defines the Lua table `tutorial_brick`
- implements the `on_double_click` item hook
- sends a message to the player who used the item

## Step 3: Register The Script In `items/init.lua`

Open:

```text
~/moongate/scripts/items/init.lua
```

Add this line with the other item script requires:

```lua
require("items.tutorial_brick")
```

Why this matters:

- creating the file is not enough
- `items/init.lua` is the place that loads item script modules into the Lua runtime

## Step 4: Validate The Templates

Run:

```bash
moongate-template validate --root-directory ~/moongate
```

The validator checks that the item template is still valid after the `scriptId` change.

## Step 5: Restart The Server

Restart the server so the template change and the new Lua file are both loaded.

This beginner path intentionally uses restart instead of hot reload.

## Step 6: Spawn And Use The Item

In game, run:

```text
.spawn_item tutorial_brick
```

Click a nearby tile to place the item, then double-click the item.

Expected result:

- the item appears where you targeted it
- double-clicking it shows `Tutorial Brick script executed.`

## Common Mistakes

- Leaving `scriptId` as `none`
- Naming the Lua table differently from the `scriptId`
- Creating `tutorial_brick.lua` but forgetting `require("items.tutorial_brick")`
- Editing the repository copy under `moongate_data/` instead of the shard root under `~/moongate/`

## Next Step

Continue with [Create Your First Loot Container](create-your-first-loot-container.md) or
[Create Your First Gump](create-your-first-gump.md).

If you want a shared travel item with a dedicated gump and a shard-wide destination list, continue with
[Public Moongates](public-moongates.md).
