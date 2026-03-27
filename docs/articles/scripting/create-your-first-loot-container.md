# Create Your First Loot Container

This guide creates a chest that rolls loot the first time a player opens it.

By the end, you will:

- create one loot template
- create one container item template that references it
- validate both files with `moongate-template`
- spawn the chest and see generated loot in game

## Before You Start

Finish this guide first:

- [Create Your First Item Template](create-your-first-item-template.md)

You also need:

- your shard root is `~/moongate`
- `moongate-template` is installed
- your server is configured to run against `~/moongate`
- you can log in with an account that can run `.spawn_item`

## Step 1: Create The Loot Template

Create this folder:

```text
~/moongate/templates/loot/tutorial/
```

Inside it, create:

```text
~/moongate/templates/loot/tutorial/first_loot_table.json
```

Paste this content:

```json
[
  {
    "type": "loot",
    "id": "tutorial_chest_basic",
    "name": "Tutorial Chest Basic",
    "category": "tutorial",
    "description": "My first loot table.",
    "rolls": 2,
    "noDropWeight": 0,
    "entries": [
      {
        "itemTemplateId": "gold",
        "weight": 5,
        "amount": 50
      },
      {
        "itemTemplateId": "torch",
        "weight": 2,
        "amount": 1
      }
    ]
  }
]
```

What this does:

- the table id is `tutorial_chest_basic`
- the chest will roll twice
- each roll picks one weighted entry from the list

## Step 2: Create The Chest Item Template

Create:

```text
~/moongate/templates/items/tutorial/first_loot_chest.json
```

Paste this content:

```json
[
  {
    "type": "item",
    "id": "tutorial_loot_chest",
    "name": "Tutorial Loot Chest",
    "category": "tutorial",
    "description": "My first loot container.",
    "tags": ["tutorial", "container", "loot"],
    "itemId": "0x0E80",
    "hue": "0",
    "goldValue": "0",
    "weight": 1,
    "scriptId": "none",
    "isMovable": true,
    "container": [],
    "containerLayoutId": "metal_chest",
    "weightMax": 40000,
    "maxItems": 125,
    "lootTables": ["tutorial_chest_basic"]
  }
]
```

Why these extra fields matter:

- `container`: marks the item as a container template
- `containerLayoutId`: must match a valid container layout
- `lootTables`: points to the loot template you created in step 1

## Step 3: Validate Both Templates

Run:

```bash
moongate-template validate --root-directory ~/moongate
```

Stop here if validation fails. Loot containers depend on both the item template and the loot template being valid.

## Step 4: Restart The Server

Restart the server so the new templates are loaded.

## Step 5: Spawn The Chest And Open It

In game, run:

```text
.spawn_item tutorial_loot_chest
```

Click a nearby tile to place the chest, then double-click the chest to open it.

Expected result:

- the first open generates loot into the container
- you should see a couple of items based on the weighted table
- opening the same chest again reuses the generated contents instead of rolling from scratch

## Common Mistakes

- Writing the loot template under the wrong tree instead of `~/moongate/templates/loot/`
- Forgetting `containerLayoutId`
- Referencing a loot table id that does not exist
- Expecting loot generation on chest spawn instead of on first open

## Next Step

Continue with [Create Your First Scheduled Event](create-your-first-scheduled-event.md).
