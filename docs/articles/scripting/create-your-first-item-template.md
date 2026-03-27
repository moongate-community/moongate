# Create Your First Item Template

This guide walks through the smallest useful Moongate item template from zero.

By the end, you will:

- create one JSON file under your shard root
- validate it with `moongate-template`
- spawn the item in game with `.spawn_item`

## Before You Start

- your shard root is `~/moongate`
- `moongate-template` is installed
- your server is configured to run against `~/moongate`
- you can log in with an account that can run `.spawn_item` on your shard

## Step 1: Create A Tutorial Folder

Create this folder in your shard root:

```text
~/moongate/templates/items/tutorial/
```

Inside it, create:

```text
~/moongate/templates/items/tutorial/first_item.json
```

## Step 2: Paste A Complete Minimal Template

Put this content in the file:

```json
[
  {
    "type": "item",
    "id": "tutorial_brick",
    "name": "Tutorial Brick",
    "category": "tutorial",
    "description": "My first Moongate item template.",
    "tags": ["tutorial", "test"],
    "itemId": "0x1F9E",
    "hue": "0",
    "goldValue": "0",
    "weight": 1,
    "scriptId": "none",
    "isMovable": true
  }
]
```

This file is a JSON array with one item template object inside it. Keep the outer `[` and `]`.

Why these fields matter:

- `type`: tells Moongate this document contains an item template
- `id`: the template id you will use later with `.spawn_item`
- `name`: the in-game display name
- `category`: groups the template for humans and tooling
- `description`: short human-facing note about what this template is for
- `tags`: optional labels you can use later for organization or loot/tag-based selection
- `itemId`: the Ultima Online art/body id for the item
- `hue`: the color; `0` means default hue
- `goldValue`: economic value metadata; `0` is fine for a tutorial item
- `weight`: item weight
- `scriptId`: `none` means this first tutorial item does not use Lua hooks yet
- `isMovable`: whether players can move the item

## Step 3: Validate The Template

Run:

```bash
moongate-template validate --root-directory ~/moongate
```

Expected result:

- the command exits with success
- the validator reports the shard root and the validation summary

If validation fails, stop and fix the file before trying to spawn the item.

## Step 4: Restart The Server

If your server is already running, restart it so the tutorial template is definitely loaded.

This guide intentionally uses restart instead of more advanced reload flows.

## Step 5: Spawn The Item In Game

Log in with an account that can run in-game admin item commands and type:

```text
.spawn_item tutorial_brick
```

Then click a nearby ground tile.

Expected result:

- the item appears at the location you clicked
- single-clicking it shows the name `Tutorial Brick`

## Common Mistakes

- Forgetting the outer JSON array `[` `]`
- Using a duplicate `id` that already exists elsewhere in your shard
- Writing `itemId` without quotes or without the `0x` format
- Validating the wrong root directory

## Next Step

If you want to keep going into NPC authoring, continue with
[Create Your First NPC Brain](create-your-first-npc-brain.md).
