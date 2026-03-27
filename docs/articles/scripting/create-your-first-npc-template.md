# Create Your First NPC Template

This guide creates a minimal NPC template and binds it to the Lua brain from the previous tutorial.

By the end, you will:

- create one mobile template JSON file
- validate it with `moongate-template`
- spawn the NPC in game with `.add_npc`
- confirm that the Lua brain is actually running

## Before You Start

Finish this guide first:

- [Create Your First NPC Brain](create-your-first-npc-brain.md)

That guide creates and registers the `tutorial_cat_brain` Lua table used below.

You also need:

- your shard root is `~/moongate`
- `moongate-template` is installed
- your server is configured to run against `~/moongate`
- you can log in with an account that can run `.add_npc` on your shard

## Step 1: Create The Template File

Create this folder:

```text
~/moongate/templates/mobiles/tutorial/
```

Inside it, create:

```text
~/moongate/templates/mobiles/tutorial/first_npc.json
```

## Step 2: Paste A Complete Minimal NPC Template

Put this content in the file:

```json
[
  {
    "type": "mobile",
    "id": "tutorial_cat",
    "category": "tutorial",
    "description": "My first scripted Moongate NPC.",
    "tags": ["tutorial", "animals", "test"],
    "ai": {
      "brain": "tutorial_cat_brain",
      "fightMode": "none",
      "rangePerception": 16,
      "rangeFight": 1
    },
    "name": "Tutorial Cat",
    "title": "your first scripted NPC",
    "variants": [
      {
        "appearance": {
          "body": "0x00C9",
          "skinHue": 779,
          "hairHue": 0,
          "hairStyle": 0
        },
        "equipment": []
      }
    ]
  }
]
```

This file is a JSON array with one mobile template object inside it. Keep the outer `[` and `]`.

What the important fields mean:

- `type`: this document contains mobile templates
- `id`: the template id you will use with `.add_npc`
- `ai.brain`: the Lua table name that will drive the NPC behavior
- `fightMode`, `rangePerception`, `rangeFight`: standard AI fields used by current mobile templates
- `name` and `title`: the in-game name shown to players
- `variants`: appearance and equipment choices for the mobile
- `appearance.body`: the UO body id
- `equipment`: starting worn or carried equipment for that variant; empty is fine for a tutorial cat

If you later point a template at the `guard` brain, patrol stays opt-in and uses the same `params` shape documented in [NPC Behaviors](npc-behaviors.md#optional-patrol-params):

```json
{
  "params": {
    "patrol_mode": { "type": "string", "value": "random_roam" },
    "patrol_radius": { "type": "string", "value": "6" }
  }
}
```

`patrol_radius` stays string-backed in template JSON because mobile params do not have a separate numeric param type.

The tutorial cat above does not use these settings, and the current production guard templates do not set them either.

## Step 3: Validate The Template

Run:

```bash
moongate-template validate --root-directory ~/moongate
```

Expected result:

- validation completes successfully

If the validator fails, fix that before trying to spawn the NPC.

## Step 4: Restart The Server

If the server is already running, restart it so both the template and the Lua brain are definitely loaded.

## Step 5: Spawn The NPC In Game

Log in with an account that can run the in-game NPC spawn command and type:

```text
.add_npc tutorial_cat
```

Then click a nearby ground tile.

Expected result:

- the NPC appears where you clicked
- after a few seconds, it says `Hello! My first Moongate brain is running.`
- if you say `hello` near it, it replies `Hello back to you.`

## Common Mistakes

- Typo in `ai.brain`
- Forgetting to register the Lua brain in `~/moongate/scripts/ai/init.lua`
- Editing the wrong tree (`moongate_data/` in the repo instead of `~/moongate/` in your shard root)
- Forgetting the outer JSON array

## Next Step

Once this works, move to the reference docs for deeper behavior work:

- [NPC Behaviors](npc-behaviors.md)
- [Scripting API Reference](api.md)
