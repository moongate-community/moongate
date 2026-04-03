# Public Moongate Design

Date: 2026-03-31

## Goal

Add a public moongate experience similar to ModernUO:

- double click a moongate
- open a shared destination gump
- choose a destination from a shard-wide list
- teleport with standard effects and sound

The feature must fit Moongate's existing Lua-first content model and must not break the current single-destination teleporter item flow.

## Scope

### In scope

- dedicated `public_moongate` item behavior
- shared Lua-authored destination list
- shared moongate gump
- double-click interaction
- runtime revalidation before teleport
- documentation for authoring the destination list

### Out of scope

- `OnMoveOver` activation
- per-item custom destination lists
- expansion/client-flag filtering like ModernUO
- murderer/young-player/sigil restrictions
- automatic world generation of public moongates

## Current State

Moongate currently has a generic teleporter item:

- template: `moongate_data/templates/items/base/teleports.json`
- script: `moongate_data/scripts/items/teleport.lua`

That script reads `point_dest` and `map_dest` from the item and teleports immediately. It does not offer a destination menu and should remain unchanged.

Also note that the magic system already spawns the existing `moongate` template for `Gate Travel`, so the public-moongate flow must not repurpose the current `moongate` template id.

ModernUO uses a separate public moongate item:

- `PublicMoongate` opens a shared `MoongateGump`
- destinations are drawn from a shared list (`PMList`)
- travel is revalidated on gump response

Moongate should mirror that separation of concerns, but using Lua for authoring and gump layout.

## Architecture

### Item split

Keep two distinct behaviors:

- `items.teleport`: generic single-destination teleporter
- `items.public_moongate`: shared-list moongate

This avoids overloading `items.teleport` with a second, unrelated mode.

### Data source

Author public moongate destinations in Lua as a shared dataset, for example:

- `moongate_data/scripts/moongates/data.lua`

The dataset is global for the shard and not stored per item instance.

Suggested shape:

```lua
local public_moongates = {}

function public_moongates.load()
  return {
    {
      id = "britannia",
      name = "Britannia",
      destinations = {
        { id = "moonglow", name = "Moonglow", map = "felucca", x = 4467, y = 1283, z = 5 },
        { id = "britain", name = "Britain", map = "felucca", x = 1336, y = 1997, z = 5 }
      }
    },
    {
      id = "ilshenar",
      name = "Ilshenar",
      destinations = {
        { id = "compassion", name = "Compassion", map = "ilshenar", x = 1215, y = 467, z = -13 }
      }
    }
  }
end

return public_moongates
```

### Gump

Add a shared gump, for example:

- `moongate_data/scripts/gumps/moongates/public_moongate.lua`

The gump should:

- render category/group navigation on the left
- render destinations for the selected group on the right
- reopen itself on navigation clicks
- execute travel immediately when a destination button is selected

This should reuse existing Moongate Lua gump patterns, especially the controller/state/render split already used by the teleport browser and GM menu.

### Interaction flow

1. Player double-clicks a `public_moongate`.
2. `items.public_moongate` validates the actor and opens the shared gump.
3. The gump loads the shard-wide dataset from Lua.
4. Player selects a destination.
5. The handler revalidates:
   - the player still exists
   - the moongate still exists
   - the player is still in range
   - the selected destination still exists in the dataset
   - the player is not already at that destination
6. Teleport executes with the same visible behavior used by `items.teleport`.

## Runtime Rules

V1 travel rules are intentionally small:

- require player mobile
- require range `<= 1` tile from the source moongate when opening and when confirming
- reject travel if source moongate is gone
- reject travel if the destination id is not found
- reject travel if the player is already at destination tile

V1 does not enforce ModernUO-specific criminal/combat/spell restrictions unless an existing Moongate utility already makes that trivial. The priority is shipping the public destination gump cleanly first.

## Content Changes

### Templates

Introduce a dedicated public moongate template, for example `public_moongate`, that resolves to:

- `scriptId = "items.public_moongate"`

It may reuse the same art (`0x0F6C`) as the current moongate item, but it must not replace the existing `moongate` template because that id is already used by spell-created moongates.

### Scripts

Add:

- `moongate_data/scripts/items/public_moongate.lua`
- `moongate_data/scripts/gumps/moongates/public_moongate.lua`
- supporting Lua modules under `moongate_data/scripts/gumps/moongates/`
- `moongate_data/scripts/moongates/data.lua`

## UX Notes

- V1 is double-click only.
- No move-over activation.
- No second confirmation gump after choosing a destination.
- The destination gump should be visually closer to Moongate's existing Lua gumps than to a pixel-perfect clone of ModernUO's C# gump.

## Testing

Expected verification:

- targeted Lua runtime test for opening the moongate gump
- targeted Lua runtime test for selecting a destination
- `luac -p` on the new Lua files
- `dotnet test` for the affected test slice

If template data is changed under the shard root outside the repo, also run:

```bash
moongate-template validate --root-directory ~/moongate
```

## Documentation

Add a short operator/content authoring guide that explains:

- what `public_moongate` is
- where the destination list lives
- how to add/remove destinations
- how it differs from a normal teleporter

## Rollout Recommendation

Ship in one focused feature branch:

1. dataset + gump modules
2. public moongate item script
3. template wiring
4. runtime tests
5. docs

This keeps the change narrow and avoids dragging in broader travel-rule parity with ModernUO before the basic public moongate workflow exists.
