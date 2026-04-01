# Public Moongates

Moongate public moongates are a separate item flow from generic teleporters.

- `public_moongate` opens a shared destination gump
- destinations are authored once for the shard
- players double click the moongate and choose where to go

This is different from the existing `moongate` template, which is still used by spell-created moongates such as `Gate Travel`.

## Authoring Model

The shared destination list lives in:

- `moongate_data/scripts/moongates/data.lua`

That file returns grouped destinations. The shipped defaults mirror the classic ModernUO public network for Felucca and Ilshenar. For example:

```lua
local data = {}

function data.load()
  return {
    {
      id = "felucca",
      name = "Felucca",
      destinations = {
        { id = "moonglow", name = "Moonglow", map = "felucca", x = 4467, y = 1283, z = 5 },
        { id = "britain", name = "Britain", map = "felucca", x = 1336, y = 1997, z = 5 }
      }
    }
  }
end

return data
```

Each destination entry uses:

- `id`: stable destination key
- `name`: player-facing label in the gump
- `map`: map name or id-like value resolved through the `map` script module
- `x`, `y`, `z`: world coordinates

## Item Template

The public gump flow uses the `public_moongate` template:

```json
{
  "id": "public_moongate",
  "base_item": "teleporter",
  "itemId": "0x0F6C",
  "scriptId": "items.public_moongate"
}
```

Use this when you want a shard-wide moongate network.

Use `teleporter` or other single-destination teleporter templates when the item should go to exactly one configured location.

## Runtime Behavior

On double click:

1. the item validates that the player is next to the moongate
2. the shared gump opens
3. the player chooses a destination
4. the selection is revalidated against the live item, player range, and current destination dataset
5. the player teleports

The current V1 flow is intentionally simple:

- double click only
- no move-over activation
- no expansion-specific destination filtering
- no special criminal or young-player rules

## Files Involved

- `moongate_data/scripts/items/public_moongate.lua`
- `moongate_data/scripts/gumps/moongates/public_moongate.lua`
- `moongate_data/scripts/gumps/moongates/constants.lua`
- `moongate_data/scripts/gumps/moongates/state.lua`
- `moongate_data/scripts/gumps/moongates/ui.lua`
- `moongate_data/scripts/gumps/moongates/render.lua`
- `moongate_data/scripts/moongates/data.lua`

## Recommended Workflow

When you change the shared destinations:

1. update `moongate_data/scripts/moongates/data.lua`
2. reload scripts or restart the server
3. verify the gump still opens and the destinations still travel correctly
