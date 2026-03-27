# Loot Containers

Moongate supports container loot that appears the first time a player opens the container, plus optional refill behavior after the container has been emptied.

This page documents the current runtime behavior for item templates and loot templates only. For API signatures, see the [Scripting API Reference](api.md).

## How The Pieces Connect

Loot containers are driven by two template types:

- item templates declare `lootTables`
- loot templates declare the actual entries to roll

At runtime, the server resolves the opened container back to its item template through the internal `item_template_id` custom value. If the container is not a container, has no template id, or has no loot tables, nothing is generated.

Item template validation also requires container templates to have a valid `containerLayoutId`. If you inherit from a base item template, an empty child `lootTables` list inherits the parent list.

## Runtime Behavior

When a player double-clicks a container, the interaction flow calls the loot generator before the container window is shown.

Current behavior:

- first open generates loot once and marks the container with `loot_generated = true`
- generated items are persisted as normal container contents
- subsequent opens reuse the existing contents
- if the container is refillable, refill is lazy rather than timer-driven
- the server only checks refill readiness when the container is opened again

Refill behavior depends on two item template params:

- `loot_refillable` must be `true`
- `loot_refill_seconds` must be a positive integer

When a refillable container becomes empty, the server records `loot_refill_ready_at_utc`. On a later open, if that timestamp has passed, loot is generated again. If the container still has any items inside, refill does not happen.

If `loot_refill_seconds` is missing, invalid, or non-positive, the container behaves as non-refillable.

## Loot Table Rules

Loot tables currently support weighted generation for loot containers:

- `rolls` defaults to `1`
- `noDropWeight` adds blank rolls into the weighted pool
- each entry must define exactly one of:
  - `itemTemplateId`
  - `itemId`
  - `itemTag`

`itemTemplateId` creates an item from another item template. `itemTag` picks a random item template that carries the tag, then creates that template. `itemId` creates a raw static item and fills name, weight, and stackability from tile data, which is mainly useful for imported legacy data.

Each roll resolves independently, so a table with `rolls: 2` can generate two items, two stackables, or one item and one blank result depending on weights.

## Real Template Shape

The repository includes test fixtures that use the same shape operators should use for shard content.

```json
[
  {
    "type": "item",
    "id": "loot_test_chest",
    "name": "Loot Test Chest",
    "category": "Test Containers",
    "description": "Spawnable chest used to verify first-open loot table generation.",
    "itemId": "0x0E80",
    "container": [],
    "lootTables": ["loot_test_chest_basic"],
    "containerLayoutId": "metal_chest",
    "weight": 1.0,
    "weightMax": 40000,
    "maxItems": 125,
    "params": {
      "loot_refillable": {
        "type": "string",
        "value": "true"
      },
      "loot_refill_seconds": {
        "type": "string",
        "value": "300"
      }
    },
    "goldValue": "0",
    "hue": "0",
    "scriptId": "none",
    "isMovable": true,
    "tags": ["container", "test", "loot"]
  }
]
```

```json
[
  {
    "type": "loot",
    "id": "loot_test_chest_basic",
    "name": "Loot Test Chest Basic",
    "category": "Test",
    "description": "Basic weighted loot table for first-open chest testing.",
    "rolls": 2,
    "noDropWeight": 0,
    "entries": [
      {
        "itemTemplateId": "gold",
        "weight": 5,
        "amount": 125
      },
      {
        "itemTag": "resources",
        "weight": 3,
        "amount": 25
      },
      {
        "itemTemplateId": "lockpick",
        "weight": 2,
        "amount": 5
      },
      {
        "itemTemplateId": "torch",
        "weight": 1,
        "amount": 1
      }
    ]
  }
]
```

## Operator Guidance

- Use `lootTables` on container item templates, not on arbitrary items.
- Make sure the container spawns from a template so `item_template_id` is present at runtime.
- Keep `containerLayoutId` valid for the chosen container body, or validation will fail at startup.
- Prefer `itemTemplateId` for shard-authored loot. Use `itemTag` when you want a random member of a tagged family. Reserve `itemId` for imported datasets that still depend on raw tile ids.
- If you want refill behavior, test the empty-container path. Refill does not begin until the last item is removed.

## Caveats

- The server does not refill containers on a background timer.
- A container that rolls empty on first open is still marked as generated.
- Internal loot metadata is managed by the server; operators should not set `loot_generated` or `loot_refill_ready_at_utc` by hand unless they are debugging a broken container state.
- The same loot template system is also reused for other runtime loot flows, but this page only covers container opening and refill behavior.
