# Vendor Sell Templates and Context Menus

This page documents the current Moongate v2 flow for:

- sell profile templates (sometimes referred to as "sell templates")
- standard context menu entries (paperdoll, buy, sell)
- custom context menu entries from Lua brain scripts

## Quick Model

1. Define a sell profile in `moongate_data/templates/sell_profiles/*.json`.
2. Link it from a mobile template using `sellProfileId`.
3. When the client requests context menu (`0xBF/0x13`), Moongate sends popup entries (`0xBF/0x14`).
4. When the player selects one (`0xBF/0x15`):
   - native entries publish server events (`VendorBuyRequestedEvent`, `VendorSellRequestedEvent`)
   - custom script entries invoke Lua callback on the mobile brain table.

## Sell Profile Template Format

Sell profiles are loaded by `SellProfileTemplateLoader` from:

- `moongate_data/templates/sell_profiles`

Example:

```json
[
  {
    "type": "sell_profile",
    "id": "vendor.basic",
    "name": "Basic Vendor",
    "category": "vendors",
    "description": "Base sell profile for generic vendors.",
    "vendorItems": [
      {
        "itemTemplateId": "apple",
        "price": 5,
        "maxStock": 20,
        "enabled": true
      }
    ],
    "acceptedItems": [
      {
        "itemTemplateId": "apple",
        "price": 2,
        "enabled": true
      }
    ]
  }
]
```

## Linking a Mobile to a Sell Profile

Use `sellProfileId` in the mobile template:

```json
[
  {
    "type": "mobile",
    "id": "vendor_test",
    "title": "Vendor",
    "sellProfileId": "vendor.basic",
    "ai": {
      "brain": "ai_vendor",
      "fightMode": "none",
      "rangePerception": 2,
      "rangeFight": 1
    },
    "variants": [
      {
        "name": "default",
        "weight": 1,
        "appearance": {
          "body": "0x0190"
        },
        "equipment": []
      }
    ]
  }
]
```

At spawn time, `MobileFactoryService` validates the profile id and stores it on the runtime mobile custom props as:

- `sell_profile_id`

This flag drives vendor `buy/sell` entries in context menu.

## Standard Context Menu Entries

`ContextMenuService` builds entries as follows:

- always: paperdoll (`tag=1`, cliloc `3006123`)
- if `sell_profile_id` is present:
  - buy (`tag=2`, cliloc `3006103`)
  - sell (`tag=3`, cliloc `3006104`)

Selection routing:

- `tag=1`: send `PaperdollPacket`
- `tag=2`: publish `VendorBuyRequestedEvent`
- `tag=3`: publish `VendorSellRequestedEvent`

## Custom Context Menus from Lua Brain

Custom entries are provided by the NPC brain table.

Supported hooks on the brain table:

- `get_context_menus(payload)` -> list of entries
- `on_selected_context_menu(menu_key, payload)` -> callback on selection
- fallback event path: `on_event("context_menu_selected", target_mobile_id, payload)` if explicit callback is missing

### `get_context_menus` return shapes

Each list item can be:

- object/table: `{ key = "follow_me", cliloc_id = 3006135 }`
- tuple table: `{ "follow_me", 3006135 }`

Moongate assigns runtime tags starting from `1000` and maps the selected tag back to your `key`.

Note: packet `0xBF/0x14` is cliloc-based. `cliloc_id` is required and must be `>= 3000000`.

### Common Cliloc IDs for Context Menus

Use these as practical defaults when defining custom entries:

| Label (client) | Cliloc ID |
| --- | --- |
| Open Paperdoll | `3006123` |
| Buy | `3006103` |
| Sell | `3006104` |
| Open Bank | `3006105` |
| Eat | `3006135` |
| Open Backpack | `3006145` |

Example:

```lua
local cliloc = require("common.cliloc_ids")

function vendor_brain.get_context_menus(ctx)
    return {
        { key = "give_food", cliloc_id = cliloc.eat },
        { key = "open_pack", cliloc_id = cliloc.open_backpack }
    }
end
```

### Payload shape

`payload` currently includes:

- `target_mobile_id`
- `session_id`
- `menu_key` (only for selection callback)
- `requester` (optional table):
  - `mobile_id`
  - `name`
  - `map_id`
  - `location = { x, y, z }`

## Complete Lua Example

File: `moongate_data/scripts/ai/vendor_brain.lua`

```lua
vendor_brain = {}
local cliloc = require("common.cliloc_ids")

function vendor_brain.on_think(npc_id)
    while true do
        coroutine.yield(250)
    end
end

function vendor_brain.get_context_menus(ctx)
    return {
        { key = "greet", cliloc_id = cliloc.eat },
        { key = "where_bank", cliloc_id = cliloc.open_backpack }
    }
end

function vendor_brain.on_selected_context_menu(menu_key, ctx)
    local npc = mobile.get(ctx.target_mobile_id)
    if not npc then
        return
    end

    if menu_key == "greet" then
        npc:say("Welcome, traveler.")
        return
    end

    if menu_key == "where_bank" then
        npc:say("The bank is to the east.")
    end
end
```

## Runtime Guards and Limits

- Context menu flow is enabled only for clients supporting modern protocol flags (SA+ path).
- Interaction range is `18` tiles for regular accounts.
- `GameMaster` (and higher) bypasses range checks.
- Custom entries are capped (current implementation limit: `32`).

## Validation Rules

Startup validation checks:

- `mobile.sellProfileId` must exist in loaded sell profiles.
- `vendorItems[].itemTemplateId` and `acceptedItems[].itemTemplateId` must resolve to valid item templates.
- `price` and `maxStock` cannot be negative.

If invalid, startup fails during `TemplateValidationLoader`.
